using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class PlatformProvisioningIntentConfiguration : IEntityTypeConfiguration<PlatformProvisioningIntent>
{
    public void Configure(EntityTypeBuilder<PlatformProvisioningIntent> builder)
    {
        builder.ToTable("platform_provisioning_intent", DatabaseSchemas.Tenancy);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ActorId).HasColumnName("actor_id");
        builder.Property(x => x.Key).HasColumnName("intent_key");
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<short>();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.HasIndex(x => new { x.ActorId, x.Key }).IsUnique().HasDatabaseName("ux_platform_provisioning_actor_key");
        builder.HasOne<PlatformUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
