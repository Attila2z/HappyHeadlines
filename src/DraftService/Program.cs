using Microsoft.EntityFrameworkCore;
using DraftService.Data;
using Shared.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for centralized logging to ELK Stack
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

// Register database
builder.Services.AddDbContext<DraftDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=drafts;Username=postgres;Password=postgres")
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate on startup
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


