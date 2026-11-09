using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class GoogleLoginTransactionConfiguration : IEntityTypeConfiguration<GoogleLoginTransaction>
{
    public void Configure(EntityTypeBuilder<GoogleLoginTransaction> entity)
    {
        entity.ToTable("GoogleLoginTransactions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasMaxLength(64);
        entity.Property(x => x.TenantId).IsRequired();
        entity.Property(x => x.Purpose).HasMaxLength(16).IsRequired();
        entity.Property(x => x.BrowserProofHash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ProtectedPayload).IsRequired();
        entity.HasIndex(x => x.ExpiresAt);
    }
}
