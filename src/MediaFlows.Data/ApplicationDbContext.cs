using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<AssetVersion> AssetVersions => Set<AssetVersion>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.Entity<MediaAsset>().HasQueryFilter(a => !a.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is IHasTimestamps timestamped)
            {
                timestamped.UpdatedAt = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                    timestamped.CreatedAt = DateTime.UtcNow;
            }
        }

        return await base.SaveChangesAsync(ct);
    }
}
