using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MediaFlows.Tests.Common;

public class TestDbContext : ApplicationDbContext
{
    public TestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore NpgsqlTsVector properties not supported by InMemory provider
        modelBuilder.Entity<MediaAsset>().Ignore(e => e.SearchVector);
        modelBuilder.Entity<AuditLog>().Ignore(e => e.SearchVector);

        // Ignore MediaMetadata complex JSON property which uses ToJson(),
        // OwnsMany and OwnsOne that are not supported by InMemory provider
        modelBuilder.Entity<MediaAsset>().Ignore(e => e.Metadata);
    }
}

public class TestModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => (context.GetType(), designTime);
}
