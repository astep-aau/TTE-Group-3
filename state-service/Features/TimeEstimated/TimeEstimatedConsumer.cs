using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using StateService.Infrastructure.Messaging;
using RabbitMQ.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using StateService.Infrastructure.Observability;
using System.Diagnostics;

namespace StateService.Features.TimeEstimated
{
    public class TimeEstimatedConsumer : BackgroundService
    {
        private readonly IRabbitMqConnection _connection;
        private readonly IServiceProvider _sp;
        private readonly ILogger<TimeEstimatedConsumer> _logger;
        private IModel? _channel;
        private const string QueueName = "time-estimated";

        public TimeEstimatedConsumer(IRabbitMqConnection connection, IServiceProvider sp, ILogger<TimeEstimatedConsumer> logger)
        {
            _connection = connection;
            _sp = sp;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _channel = _connection.CreateChannel();
            _channel.BasicQos(0, 10, false);
            
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                var sw = Stopwatch.StartNew();
                using var activity = Infrastructure.Observability.ActivitySourceHolder.Source.StartActivity("consume.time-estimated");
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var msg = JsonSerializer.Deserialize<TimeEstimatedMessage>(json);
                    if (msg is null)
                    {
                        _logger.LogWarning("Null message received on {Queue}", QueueName);
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }
                    activity?.AddTag("pid", msg.Pid);
                    activity?.AddTag("correlationId", msg.CorrelationId);

                    using var scope = _sp.CreateScope();
                    var validator = scope.ServiceProvider.GetRequiredService<IValidator<TimeEstimatedMessage>>();
                    var validationResult = await validator.ValidateAsync(msg, stoppingToken);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogWarning("Validation failed pid={Pid} errors={Errors}", msg.Pid, string.Join(";", validationResult.Errors.Select(e => e.ErrorMessage)));
                        _channel.BasicAck(ea.DeliveryTag, false);
                        MetricsRegistry.MessagesFailed.Add(1);
                        return;
                    }
                    var handler = scope.ServiceProvider.GetRequiredService<TimeEstimatedHandler>();
                    await handler.HandleAsync(msg, stoppingToken);
                    _channel.BasicAck(ea.DeliveryTag, false);
                    MetricsRegistry.MessagesConsumed.Add(1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message on {Queue}");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                    MetricsRegistry.MessagesFailed.Add(1);
                }
                finally
                {
                    sw.Stop();
                    MetricsRegistry.MessageProcessingMs.Record(sw.Elapsed.TotalMilliseconds);
                }
            };

            _channel.BasicConsume(QueueName, autoAck: false, consumer);
            _logger.LogInformation("Consuming queue {Queue}", QueueName);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _channel?.Dispose();
        }
    }
}
