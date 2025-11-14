using System.Text;
using RabbitMQ.Client;

namespace TrainingService.Services
{
    public class RabbitMqPublisher
    {
        private readonly ConnectionFactory _factory;

        public RabbitMqPublisher()
        {
            _factory = new ConnectionFactory()
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest"
            };
        }

        public void Publish(string routingKey, string message)
        {
            using var connection = _factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ExchangeDeclare(
                exchange: "TrainingService-exchange",
                type: ExchangeType.Topic,
                durable: true
            );

            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(
                exchange: "TrainingService-exchange",
                routingKey: routingKey,
                basicProperties: null,
                body: body
            );
        }
    }
}
