using Microsoft.EntityFrameworkCore;
using DraftService.Data;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Debug()
        .Enrich.WithProperty("service", "DraftService")
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
        .WriteTo.Http(
            "http://logstash:5000",
            queueLimitBytes: 1024 * 1024,
            textFormatter: new Serilog.Formatting.Compact.CompactJsonFormatter(),
            period: TimeSpan.FromSeconds(5)
        );
});

builder.Services.AddDbContext<DraftDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=drafts;Username=postgres;Password=postgres")
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("DraftService"))
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
    var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("DraftDatabase migrated successfully");
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();


