using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace StateService.Infrastructure.Messaging
{
    public interface IRabbitMqConnection : IDisposable
    {
        IModel CreateChannel();
        bool IsConnected { get; }
    }

    public class RabbitMqConnection : IRabbitMqConnection
    {
        private readonly ILogger<RabbitMqConnection> _logger;
        private readonly ConnectionFactory _factory;
        private IConnection? _connection;
        private readonly object _syncRoot = new();
        private readonly ConcurrentDictionary<int, IModel> _channels = new();
        private readonly string[] _queues = new[]
        {
            "estimation-requested", "route-calculated", "time-estimated", "process-finished"
        };

        public bool IsConnected => _connection?.IsOpen == true;

        public RabbitMqConnection(ILogger<RabbitMqConnection> logger, IConfiguration configuration)
        {
            _logger = logger;
            _factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "localhost",
                UserName = configuration["RabbitMQ:Username"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                DispatchConsumersAsync = true
            };
            TryConnect();
        }

        private void TryConnect()
        {
            if (IsConnected) return;
            lock (_syncRoot)
            {
                int attempt = 0;
                while (!IsConnected && attempt < 5)
                {
                    attempt++;
                    try
                    {
                        _connection = _factory.CreateConnection();
                        _connection.ConnectionShutdown += OnConnectionShutdown;
                        _logger.LogInformation("RabbitMQ connected host={Host} attempt={Attempt}", _factory.HostName, attempt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RabbitMQ connect failed host={Host} attempt={Attempt}", _factory.HostName, attempt);
                        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    }
                }
                if (!IsConnected)
                {
                    _logger.LogError("RabbitMQ connection could not be established to host={Host}", _factory.HostName);
                }
            }
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown: replyCode={Code} text={Text}", e.ReplyCode, e.ReplyText);
            TryConnect();
        }

        public IModel CreateChannel()
        {
            if (!IsConnected)
            {
                _logger.LogWarning("CreateChannel invoked while disconnected. Attempting reconnect.");
                TryConnect();
            }
            if (!IsConnected)
            {
                throw new InvalidOperationException("RabbitMQ persistent connection not available");
            }
            var channel = _connection!.CreateModel();
            _channels[channel.ChannelNumber] = channel;
            DeclareTopology(channel);
            _logger.LogDebug("RabbitMQ channel created channelNumber={ChannelNumber}", channel.ChannelNumber);
            return channel;
        }

        private void DeclareTopology(IModel channel)
        {
            channel.ExchangeDeclare("dlx.state", ExchangeType.Direct, durable: true);
            channel.ExchangeDeclare("state.events", ExchangeType.Fanout, durable: true);

            foreach (var q in _queues)
            {
                var args = new Dictionary<string, object>
                {
                    {"x-dead-letter-exchange", "dlx.state"},
                    {"x-message-ttl", 600000}
                };
                channel.QueueDeclare(queue: q, durable: true, exclusive: false, autoDelete: false, arguments: args);
                channel.QueueBind(q, "state.events", string.Empty);
                var dlq = q + ".dlq";
                channel.QueueDeclare(dlq, durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.QueueBind(dlq, "dlx.state", string.Empty);
            }
        }

        public void Dispose()
        {
            foreach (var kv in _channels)
            {
                try { kv.Value.Dispose(); } catch { }
            }
            _channels.Clear();
            _connection?.Dispose();
            _logger.LogInformation("RabbitMQ connection disposed");
        }
    }
}
