using Microsoft.EntityFrameworkCore;
using SubscriberService.Models;

namespace SubscriberService.Data
{
    public class SubscriberDbContext : DbContext
    {
        public SubscriberDbContext(DbContextOptions<SubscriberDbContext> options)
            : base(options)
        {
        }

        public DbSet<Subscriber> Subscribers { get; set; }
    }

    public class SubscriberDbContextFactory
        : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<SubscriberDbContext>
    {
        public SubscriberDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SubscriberDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=subscribers;Username=postgres;Password=postgres")
                .Options;
            return new SubscriberDbContext(options);
        }
    }
}
