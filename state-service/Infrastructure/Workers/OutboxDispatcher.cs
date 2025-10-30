using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using StateService.Infrastructure.Persistence;
using StateService.Infrastructure.Messaging;
using RabbitMQ.Client;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace StateService.Infrastructure.Workers
{
    public class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<OutboxDispatcher> _logger;
        private readonly IRabbitMqConnection _rabbit;
        private IModel? _channel;
        private const int BatchSize = 50;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

        public OutboxDispatcher(IServiceProvider sp, ILogger<OutboxDispatcher> logger, IRabbitMqConnection rabbit)
        {
            _sp = sp;
            _logger = logger;
            _rabbit = rabbit;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _channel = _rabbit.CreateChannel();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<StateDbContext>();
                    var pending = await db.OutboxMessages
                        .Where(o => o.ProcessedAt == null)
                        .OrderBy(o => o.Id)
                        .Take(BatchSize)
                        .ToListAsync(stoppingToken);
                    if (pending.Count == 0)
                    {
                        await Task.Delay(Interval, stoppingToken);
                        continue;
                    }
                    foreach (var msg in pending)
                    {
                        var body = Encoding.UTF8.GetBytes(msg.Payload);
                        var props = _channel.CreateBasicProperties();
                        props.Persistent = true;
                        if (!string.IsNullOrWhiteSpace(msg.CorrelationId))
                            props.CorrelationId = msg.CorrelationId;
                        _channel.BasicPublish(exchange: "state.events", routingKey: string.Empty, basicProperties: props, body: body);
                        msg.ProcessedAt = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Outbox dispatched count={Count}", pending.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox dispatch cycle error");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _channel?.Dispose();
        }
    }
}
