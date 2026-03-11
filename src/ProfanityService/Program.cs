using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Debug()
        .Enrich.WithProperty("service", "ProfanityService")
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

builder.Services.AddDbContext<ProfanityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProfanityDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("ProfanityDatabase migrated successfully");

    // Seed some default profanity words if the table is empty
    if (!db.ProfanityWords.Any())
    {
        db.ProfanityWords.AddRange(
            new ProfanityService.Models.ProfanityWord { Word = "badword1" },
            new ProfanityService.Models.ProfanityWord { Word = "badword2" },
            new ProfanityService.Models.ProfanityWord { Word = "badword3" }
        );
        db.SaveChanges();
        app.Logger.LogInformation("ProfanityDatabase seeded with default words");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();