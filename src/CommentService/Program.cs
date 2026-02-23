using Microsoft.EntityFrameworkCore;
using CommentService.Data;
using CommentService.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();