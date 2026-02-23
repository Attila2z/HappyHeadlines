using Microsoft.EntityFrameworkCore;
using ProfanityService.Models;

namespace ProfanityService.Data
{
    public class ProfanityDbContext : DbContext
    {
        public ProfanityDbContext(DbContextOptions<ProfanityDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProfanityWord> ProfanityWords { get; set; }
    }

    // Needed for dotnet ef migrations add
    public class ProfanityDbContextFactory
        : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<ProfanityDbContext>
    {
        public ProfanityDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<ProfanityDbContext>()
                .UseNpgsql("Host=localhost;Port=5434;Database=profanity;Username=postgres;Password=postgres")
                .Options;
            return new ProfanityDbContext(options);
        }
    }
}