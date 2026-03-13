using Microsoft.EntityFrameworkCore;
using DraftService.Models;

namespace DraftService.Data
{
    public class DraftDbContext : DbContext
    {
        public DraftDbContext(DbContextOptions<DraftDbContext> options) : base(options)
        {
        }

        public DbSet<Draft> Drafts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Create index for faster queries
            modelBuilder.Entity<Draft>()
                .HasIndex(d => d.AuthorId);

            modelBuilder.Entity<Draft>()
                .HasIndex(d => d.Status);

            modelBuilder.Entity<Draft>()
                .HasIndex(d => d.CreatedAt);
        }
    }
}
