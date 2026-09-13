using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<CustodyEvent> CustodyEvents => Set<CustodyEvent>();
    public DbSet<CustodyTransfer> CustodyTransfers => Set<CustodyTransfer>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var dateTimeProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?));

        foreach (var property in dateTimeProperties)
        {
            property.SetValueConverter(UtcDateTimeConverter.Instance);
        }
    }
}
