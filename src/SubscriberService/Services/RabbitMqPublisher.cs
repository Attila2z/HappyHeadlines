using System.Text.Json;
using RabbitMQ.Client;
using SubscriberService.Models;

namespace SubscriberService.Services
{
    public class RabbitMqPublisher : IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly IConfiguration _config;
        private readonly ILogger<RabbitMqPublisher> _logger;
        private const string QueueName = "subscriber.welcome";

        public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
        {
            _config = config;
            _logger = logger;
            TryConnect();
        }

        private void TryConnect()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config["RabbitMQ:Host"] ?? "rabbitmq",
                    Port     = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
                    UserName = _config["RabbitMQ:Username"] ?? "guest",
                    Password = _config["RabbitMQ:Password"] ?? "guest",
                };
                _connection = factory.CreateConnection();
                _channel    = _connection.CreateModel();
                _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _logger.LogInformation("RabbitMQ connected and queue '{queue}' declared", QueueName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ unavailable — welcome messages will not be queued");
            }
        }

        public void Publish(Subscriber subscriber)
        {
            if (_channel == null || !_channel.IsOpen)
                TryConnect();

            if (_channel == null || !_channel.IsOpen)
            {
                _logger.LogWarning("RabbitMQ still unavailable — skipping publish for {email}", subscriber.Email);
                return;
            }

            try
            {
                var body  = JsonSerializer.SerializeToUtf8Bytes(subscriber);
                var props = _channel.CreateBasicProperties();
                props.Persistent = true;
                _channel.BasicPublish(exchange: "", routingKey: QueueName, basicProperties: props, body: body);
                _logger.LogInformation("Published subscriber {email} to queue '{queue}'", subscriber.Email, QueueName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish subscriber {email} to RabbitMQ", subscriber.Email);
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
