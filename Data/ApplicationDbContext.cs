using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Threading;
using smallurl.Models;

namespace smallurl.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Link> Links => Set<Link>();
        public DbSet<Click> Clicks => Set<Click>();
        public DbSet<UrlMapping> UrlMappings => Set<UrlMapping>();
        public DbSet<Concept> Concepts => Set<Concept>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Link>(e =>
            {
                e.HasKey(l => l.Id);
                e.Property(l => l.Id).ValueGeneratedOnAdd();
                e.Property(l => l.OriginalUrl).IsRequired();
                e.HasIndex(l => l.CustomSlug).IsUnique();
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

            modelBuilder.Entity<UrlMapping>(e =>
            {
                e.HasKey(u => u.Id);
                e.Property(u => u.Id).ValueGeneratedOnAdd();
                e.Property(u => u.OriginalUrl).IsRequired();
            });

            modelBuilder.Entity<Concept>(e =>
            {
                e.HasKey(c => c.Id);
                e.Property(c => c.Id).ValueGeneratedOnAdd();
                e.Property(c => c.Name).IsRequired();
                e.HasIndex(c => c.Name).IsUnique();
            });
        }

        // Basic retry for transient SQLITE_BUSY errors. Keeps transactions short and retries with backoff.
        public override int SaveChanges()
        {
            return SaveChangesWithRetry(() => base.SaveChanges());
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return SaveChangesWithRetryAsync(() => base.SaveChangesAsync(cancellationToken));
        }

        private int SaveChangesWithRetry(Func<int> saveFunc)
        {
            const int maxRetries = 5;
            var delay = 100;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return saveFunc();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
                {
                    if (attempt >= maxRetries) throw;
                    Thread.Sleep(delay);
                    delay *= 2;
                }
            }
        }

        private async Task<int> SaveChangesWithRetryAsync(Func<Task<int>> saveFunc)
        {
            const int maxRetries = 5;
            var delay = 100;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await saveFunc();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
                {
                    if (attempt >= maxRetries) throw;
                    await Task.Delay(delay);
                    delay *= 2;
                }
            }
        }
    }
}
