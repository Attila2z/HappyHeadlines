using ArticleService.Services;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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