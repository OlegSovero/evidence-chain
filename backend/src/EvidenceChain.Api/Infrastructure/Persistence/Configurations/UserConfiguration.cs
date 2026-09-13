using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EvidenceChain.Api.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table => table.HasCheckConstraint(
            "CK_Users_Role",
            "[Role] IN (N'Investigador', N'Custodio', N'Supervisor')"));

        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserName).HasMaxLength(50).IsRequired();
        builder.HasIndex(u => u.UserName).IsUnique().HasDatabaseName("UX_Users_UserName");

        builder.Property(u => u.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(20).IsRequired();
    }
}
