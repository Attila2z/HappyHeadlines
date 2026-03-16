using Microsoft.EntityFrameworkCore;
using CommentService.Data;
using CommentService.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Debug()
        .Enrich.WithProperty("service", "CommentService")
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

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CommentService"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddZipkinExporter(opt =>
            {
                opt.Endpoint = new Uri(
                    builder.Configuration["Zipkin:Endpoint"]
                        ?? "http://zipkin:9411/api/v2/spans");
            });
    });

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