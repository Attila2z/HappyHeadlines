using System.Text.Json;
using NewsletterService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NewsletterService.Services
{
    public class WelcomeMailConsumer : BackgroundService
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly IConfiguration _config;
        private readonly ILogger<WelcomeMailConsumer> _logger;
        private const string QueueName = "subscriber.welcome";

        public WelcomeMailConsumer(IConfiguration config, ILogger<WelcomeMailConsumer> logger)
        {
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Connect();
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WelcomeMailConsumer: RabbitMQ connection lost, retrying in 10 s");
                    _channel?.Dispose();
                    _connection?.Dispose();
                    _channel    = null;
                    _connection = null;
                    await Task.Delay(10_000, stoppingToken);
                }
            }
        }

        private void Connect()
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
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, ea) =>
            {
                try
                {
                    var subscriber = JsonSerializer.Deserialize<SubscriberDto>(ea.Body.Span);
                    if (subscriber != null)
                        _logger.LogInformation(
                            "Welcome email sent to {name} <{email}>",
                            subscriber.Name, subscriber.Email);

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process welcome mail message");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("WelcomeMailConsumer listening on queue '{queue}'", QueueName);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
