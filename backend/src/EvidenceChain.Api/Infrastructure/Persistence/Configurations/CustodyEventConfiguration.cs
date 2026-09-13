using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EvidenceChain.Api.Infrastructure.Persistence.Configurations;

public class CustodyEventConfiguration : IEntityTypeConfiguration<CustodyEvent>
{
    public const string AppendOnlyTrigger = "TR_CustodyEvents_AppendOnly";

    public void Configure(EntityTypeBuilder<CustodyEvent> builder)
    {
        // Declaring the trigger makes EF avoid the OUTPUT clause, which SQL Server
        // rejects on tables with triggers.
        builder.ToTable("CustodyEvents", table => table.HasTrigger(AppendOnlyTrigger));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.OccurredAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.PreviousHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(e => e.Hash).HasColumnType("binary(32)").IsRequired();

        builder.HasIndex(e => new { e.EvidenceId, e.Sequence })
            .IsUnique()
            .HasDatabaseName("UX_CustodyEvents_Evidence_Seq");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.FromCustodianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ToCustodianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustodyTransfer>().WithMany().HasForeignKey(e => e.TransferId).OnDelete(DeleteBehavior.Restrict);
    }
}
