// =============================================================================
// Data/ArticleDbContextFactory.cs
// =============================================================================
// This class exists ONLY for running migrations (dotnet ef migrations add).
// EF Core needs to create a DbContext at design time, but our Program.cs
// no longer registers ArticleDbContext directly.
// This factory tells EF Core how to create one just for migrations.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArticleService.Data
{
    public class ArticleDbContextFactory : IDesignTimeDbContextFactory<ArticleDbContext>
    {
        public ArticleDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<ArticleDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=articles;Username=postgres;Password=postgres")
                .Options;

            return new ArticleDbContext(options);
        }
    }
}