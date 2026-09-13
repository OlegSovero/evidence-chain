using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EvidenceChain.Api.Infrastructure.Persistence.Configurations;

public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ToTable("Evidence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_Evidence_Code");

        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.LastEventAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.IntegrityStatus).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(e => e.IntegrityCheckedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne(e => e.CurrentCustodian)
            .WithMany()
            .HasForeignKey(e => e.CurrentCustodianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Events)
            .WithOne()
            .HasForeignKey(ev => ev.EvidenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LastEventAtUtc, e.Id })
            .IsDescending(true, true)
            .IncludeProperties(e => new { e.Code, e.Description, e.CurrentCustodianId, e.IntegrityStatus })
            .HasDatabaseName("IX_Evidence_Keyset");

        builder.HasIndex(e => new { e.CurrentCustodianId, e.LastEventAtUtc, e.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_Evidence_Custodian_Keyset");
    }
}
