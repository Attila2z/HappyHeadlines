using Microsoft.EntityFrameworkCore;
using CommentService.Data;
using CommentService.Services;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddHttpClient<ProfanityClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ProfanityService:BaseUrl"]
            ?? "http://profanityservice:8080"
    );
});

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "redis-comment:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn + ",abortConnect=false"));

builder.Services.AddSingleton<CommentCacheService>();
builder.Services.AddSingleton<CommentCacheMetrics>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("CommentDatabase migrated successfully");
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseMetricServer();
app.UseHttpMetrics();

app.MapControllers();
app.Run();