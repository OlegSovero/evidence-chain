using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EvidenceChain.Api.Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        builder.HasKey(r => new { r.UserId, r.IdempotencyKey }).HasName("PK_Idempotency");

        builder.Property(r => r.IdempotencyKey).HasMaxLength(100);
        builder.Property(r => r.RequestHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(r => r.ResponseBody).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetime2(3)");
    }
}
