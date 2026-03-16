using ArticleService.Services;
using Prometheus;
// using Serilog;
// using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Centralized logging (Phase 4) — Commented out for compulsory assignment
// builder.Host.UseSerilog((context, configuration) =>
// {
//     configuration
//         .MinimumLevel.Debug()
//         .Enrich.WithProperty("service", "ArticleService")
//         .Enrich.FromLogContext()
//         .Enrich.WithMachineName()
//         .WriteTo.Console(new CompactJsonFormatter())
//         .WriteTo.Http(
//             "http://logstash:5000",
//             queueLimitBytes: 1024 * 1024,
//             textFormatter: new CompactJsonFormatter(),
//             period: TimeSpan.FromSeconds(5)
//         );
// });

builder.Services.AddSingleton<DatabaseRouter>();

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "redis-article:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn + ",abortConnect=false"));

builder.Services.AddSingleton<ArticleCacheMetrics>();
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

app.UseMetricServer();
app.UseHttpMetrics();

app.MapControllers();

app.Run();