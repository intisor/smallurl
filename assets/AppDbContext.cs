using Microsoft.EntityFrameworkCore;
using smallurl.Models;

namespace smallurl.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Link> Links => Set<Link>();
        public DbSet<Click> Clicks => Set<Click>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Link>(e =>
            {
                e.HasKey(l => l.Id);
                e.Property(l => l.Id).ValueGeneratedOnAdd();
                e.Property(l => l.OriginalUrl).IsRequired();
                e.HasIndex(l => l.CustomSlug).IsUnique().HasFilter("[CustomSlug] IS NOT NULL");
                e.HasMany(l => l.Clicks)
                    .WithOne(c => c.Link)
                    .HasForeignKey(c => c.LinkId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Click>(e =>
            {
                e.HasKey(c => c.Id);
                e.Property(c => c.Id).ValueGeneratedOnAdd();
            });
        }
    }
}
