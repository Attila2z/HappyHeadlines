using System.Text.Json;
using PublisherService.Models;
using RabbitMQ.Client;

namespace PublisherService.Services
{
    public class ArticleQueuePublisher : IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly IConfiguration _config;
        private readonly ILogger<ArticleQueuePublisher> _logger;
        private const string QueueName = "article.publish";

        public ArticleQueuePublisher(IConfiguration config, ILogger<ArticleQueuePublisher> logger)
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
                _logger.LogInformation("ArticleQueuePublisher connected to RabbitMQ, queue '{queue}' ready", QueueName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ unavailable — article publishing will fail until connection is restored");
            }
        }

        public bool Publish(ArticleQueueMessage message)
        {
            if (_channel == null || !_channel.IsOpen)
                TryConnect();

            if (_channel == null || !_channel.IsOpen)
            {
                _logger.LogError("Cannot publish article — RabbitMQ is unavailable");
                return false;
            }

            try
            {
                var body  = JsonSerializer.SerializeToUtf8Bytes(message);
                var props = _channel.CreateBasicProperties();
                props.Persistent = true;
                _channel.BasicPublish(exchange: "", routingKey: QueueName, basicProperties: props, body: body);
                _logger.LogInformation("Article '{title}' queued for publication in continent {continent}", message.Title, message.Continent);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish article '{title}' to RabbitMQ", message.Title);
                return false;
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
