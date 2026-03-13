

using Microsoft.EntityFrameworkCore;
using ArticleService.Models;

namespace ArticleService.Data
{
    public class ArticleDbContext : DbContext
    {
        // The constructor receives options (like the connection string)
        // from Program.cs via Dependency Injection
        public ArticleDbContext(DbContextOptions<ArticleDbContext> options)
            : base(options)
        {
        }

        // DbSet = a "table". EF Core creates an "Articles" table in PostgreSQL.
        // You use this like a list:
        //   _context.Articles.Add(...)
        //   _context.Articles.Find(id)
        //   _context.Articles.ToList()
        public DbSet<Article> Articles { get; set; }
    }
}