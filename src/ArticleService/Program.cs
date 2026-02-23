using ArticleService.Services;

var builder = WebApplication.CreateBuilder(args);

// Register the DatabaseRouter as a Singleton
// It holds all 8 database connections and routes requests to the right one
builder.Services.AddSingleton<DatabaseRouter>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate ALL 8 databases on startup
using (var scope = app.Services.CreateScope())
{
    var router = scope.ServiceProvider.GetRequiredService<DatabaseRouter>();
    router.MigrateAll();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();