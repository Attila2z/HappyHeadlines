using NewsletterService.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Debug()
        .Enrich.WithProperty("service", "NewsletterService")
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

builder.Services.AddHttpClient<SubscriberClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["SubscriberService:BaseUrl"]
            ?? "http://subscriberservice:8080");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHostedService<WelcomeMailConsumer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("NewsletterService"))
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseMetricServer();
app.UseHttpMetrics();

app.MapControllers();
app.Run();
