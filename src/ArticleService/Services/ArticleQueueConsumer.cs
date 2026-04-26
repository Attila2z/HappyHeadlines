using System.Text.Json;
using ArticleService.Models;
using ArticleService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ArticleService.Services
{
    public class ArticleQueueConsumer : BackgroundService
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ArticleQueueConsumer> _logger;
        private const string QueueName = "article.publish";

        public ArticleQueueConsumer(
            IConfiguration config,
            IServiceScopeFactory scopeFactory,
            ILogger<ArticleQueueConsumer> logger)
        {
            _config      = config;
            _scopeFactory = scopeFactory;
            _logger      = logger;
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
                    _logger.LogWarning(ex, "ArticleQueueConsumer: connection lost, retrying in 10 s");
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
                HostName             = _config["RabbitMQ:Host"] ?? "rabbitmq",
                Port                 = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
                UserName             = _config["RabbitMQ:Username"] ?? "guest",
                Password             = _config["RabbitMQ:Password"] ?? "guest",
                DispatchConsumersAsync = true,
            };

            _connection = factory.CreateConnection();
            _channel    = _connection.CreateModel();
            _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    var message = JsonSerializer.Deserialize<ArticleQueueMessage>(ea.Body.Span);
                    if (message != null)
                    {
                        using var scope  = _scopeFactory.CreateScope();
                        var router       = scope.ServiceProvider.GetRequiredService<DatabaseRouter>();
                        var contexts     = router.CreateContextsForSaving(message.Continent);

                        foreach (var ctx in contexts)
                        {
                            using var context = ctx;
                            context.Articles.Add(new Article
                            {
                                Title     = message.Title,
                                Content   = message.Content,
                                Author    = message.Author,
                                Continent = message.Continent
                            });
                            await context.SaveChangesAsync();
                        }

                        _logger.LogInformation(
                            "Article '{title}' from queue saved to {continent} database",
                            message.Title, message.Continent);
                    }

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process article from queue");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("ArticleQueueConsumer listening on queue '{queue}'", QueueName);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }

    public class ArticleQueueMessage
    {
        public string Title     { get; set; } = string.Empty;
        public string Content   { get; set; } = string.Empty;
        public string Author    { get; set; } = string.Empty;
        public string Continent { get; set; } = string.Empty;
    }
}
