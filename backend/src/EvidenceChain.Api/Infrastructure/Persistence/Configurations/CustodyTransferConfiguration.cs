using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EvidenceChain.Api.Infrastructure.Persistence.Configurations;

public class CustodyTransferConfiguration : IEntityTypeConfiguration<CustodyTransfer>
{
    public void Configure(EntityTypeBuilder<CustodyTransfer> builder)
    {
        builder.ToTable("CustodyTransfers", table => table.HasCheckConstraint(
            "CK_Transfer_DistinctCustodians",
            "[FromCustodianId] <> [ToCustodianId]"));

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Status).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(t => t.Reason).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ResponseNote).HasMaxLength(500);
        builder.Property(t => t.RequestedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(t => t.RespondedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasOne<Evidence>().WithMany().HasForeignKey(t => t.EvidenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.FromCustodianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.ToCustodianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.RespondedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.EvidenceId)
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("UX_Transfer_OnePendingPerEvidence");

        builder.HasIndex(t => new { t.Status, t.RequestedAtUtc })
            .IncludeProperties(t => new { t.EvidenceId, t.ToCustodianId })
            .HasDatabaseName("IX_Transfer_PendingAge");
    }
}
