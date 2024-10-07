using Microsoft.EntityFrameworkCore;
using smallurl.Models;

namespace smallurl.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UrlMapping> UrlMappings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UrlMapping>()
          .HasKey(u => u.Id);

            modelBuilder.Entity<UrlMapping>()
          .Property(u => u.Id)
          .ValueGeneratedOnAdd();

            //modelBuilder.Entity<UrlMapping>()
            //    .HasIndex(u => u.ShortCode)
            //    .IsUnique();
        }
    }
}
