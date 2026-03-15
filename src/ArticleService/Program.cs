using ArticleService.Services;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Debug()
        .Enrich.WithProperty("service", "ArticleService")
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(new CompactJsonFormatter())
        .WriteTo.Http(
            "http://logstash:5000",
            queueLimitBytes: 1024 * 1024,
            textFormatter: new CompactJsonFormatter(),
            period: TimeSpan.FromSeconds(5)
        );
});

// Register the DatabaseRouter as a Singleton
// It holds all 8 database connections and routes requests to the right one
builder.Services.AddSingleton<DatabaseRouter>();

// Redis connection for the article cache
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "redis-article:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn));

// Prometheus metrics counters (singleton so they accumulate across requests)
builder.Services.AddSingleton<ArticleCacheMetrics>();

// Background service that pre-fills the cache every 10 minutes
builder.Services.AddHostedService<ArticleCachePreloader>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var router = scope.ServiceProvider.GetRequiredService<DatabaseRouter>();
    router.MigrateAll();
    app.Logger.LogInformation("ArticleDatabase migrated successfully");
}

app.UseSwagger();
app.UseSwaggerUI();

// Expose /metrics for Prometheus scraping
app.UseMetricServer();
app.UseHttpMetrics();

app.MapControllers();

app.Run();