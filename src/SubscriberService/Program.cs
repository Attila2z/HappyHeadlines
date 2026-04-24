using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using SubscriberService.Data;
using SubscriberService.Models;
using SubscriberService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("featureflags.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Debug()
        .Enrich.WithProperty("service", "SubscriberService")
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

builder.Services.Configure<FeatureFlags>(builder.Configuration.GetSection("FeatureFlags"));

builder.Services.AddDbContext<SubscriberDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddSingleton<RabbitMqPublisher>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SubscriberService"))
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
    var db = scope.ServiceProvider.GetRequiredService<SubscriberDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("SubscriberDatabase migrated successfully");
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseMetricServer();
app.UseHttpMetrics();

app.MapControllers();
app.Run();
