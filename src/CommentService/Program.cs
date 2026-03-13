using Microsoft.EntityFrameworkCore;
using CommentService.Data;
using CommentService.Services;
using Serilog;
using Serilog.Formatting.Compact;

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

// Register database
builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Register ProfanityClient with HttpClient
// The URL of ProfanityService comes from appsettings.json
builder.Services.AddHttpClient<ProfanityClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ProfanityService:BaseUrl"]
            ?? "http://profanityservice:8080"
    );
});

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
app.MapControllers();
app.Run();