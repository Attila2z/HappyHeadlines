using Microsoft.EntityFrameworkCore;
using CommentService.Models;

namespace CommentService.Data
{
    public class CommentDbContext : DbContext
    {
        public CommentDbContext(DbContextOptions<CommentDbContext> options)
            : base(options)
        {
        }

        public DbSet<Comment> Comments { get; set; }
    }

    // Needed for dotnet ef migrations add
    public class CommentDbContextFactory
        : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<CommentDbContext>
    {
        public CommentDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<CommentDbContext>()
                .UseNpgsql("Host=localhost;Port=5433;Database=comments;Username=postgres;Password=postgres")
                .Options;
            return new CommentDbContext(options);
        }
    }
}