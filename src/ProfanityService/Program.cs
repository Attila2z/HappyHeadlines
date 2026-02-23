using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProfanityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate and seed some default bad words on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProfanityDbContext>();
    db.Database.Migrate();

    // Seed some default profanity words if the table is empty
    if (!db.ProfanityWords.Any())
    {
        db.ProfanityWords.AddRange(
            new ProfanityService.Models.ProfanityWord { Word = "badword1" },
            new ProfanityService.Models.ProfanityWord { Word = "badword2" },
            new ProfanityService.Models.ProfanityWord { Word = "badword3" }
        );
        db.SaveChanges();
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();