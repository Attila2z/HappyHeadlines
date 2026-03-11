using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraftService.Data
{
    public class DraftDbContextFactory : IDesignTimeDbContextFactory<DraftDbContext>
    {
        public DraftDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DraftDbContext>();
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=drafts;Username=postgres;Password=postgres";

            optionsBuilder.UseNpgsql(connectionString);

            return new DraftDbContext(optionsBuilder.Options);
        }
    }
}
