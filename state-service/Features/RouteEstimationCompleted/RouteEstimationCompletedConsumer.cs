using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StateService.Infrastructure.Messaging;
using StateService.Infrastructure.Observability;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StateService.Features.RouteEstimationCompleted
{
    public class RouteEstimationCompletedConsumer : BackgroundService
    {
        private readonly IRabbitMqConnection _connection;
        private readonly IServiceProvider _sp;
        private readonly ILogger<RouteEstimationCompletedConsumer> _logger;
        private IModel? _channel;
        private const string QueueName = "RouteEstimationCompleted";

        public RouteEstimationCompletedConsumer(IRabbitMqConnection connection, IServiceProvider sp, ILogger<RouteEstimationCompletedConsumer> logger)
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
                using var activity = ActivitySourceHolder.Source.StartActivity("consume.route-estimation-completed");
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var msg = JsonSerializer.Deserialize<RouteEstimationCompletedMessage>(json);
                    if (msg is null)
                    {
                        _logger.LogWarning("Null message received on {Queue}", QueueName);
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }

                    activity?.AddTag("correlationId", msg.CorrelationId);

                    using var scope = _sp.CreateScope();
                    var validator = scope.ServiceProvider.GetRequiredService<IValidator<RouteEstimationCompletedMessage>>();
                    var validationResult = await validator.ValidateAsync(msg, stoppingToken);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogWarning("Validation failed correlationId={CorrelationId} errors={Errors}", msg.CorrelationId, string.Join(";", validationResult.Errors.Select(e => e.ErrorMessage)));
                        _channel.BasicAck(ea.DeliveryTag, false);
                        MetricsRegistry.MessagesFailed.Add(1);
                        return;
                    }

                    var handler = scope.ServiceProvider.GetRequiredService<RouteEstimationCompletedHandler>();
                    await handler.HandleAsync(msg, stoppingToken);
                    _channel.BasicAck(ea.DeliveryTag, false);
                    MetricsRegistry.MessagesConsumed.Add(1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message on {Queue}", QueueName);
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

