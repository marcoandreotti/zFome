using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Delivery;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class DeliveryRegionConfiguration : IEntityTypeConfiguration<DeliveryRegion>
{
    public void Configure(EntityTypeBuilder<DeliveryRegion> builder)
    {
        builder.ToTable("delivery_region", DatabaseSchemas.Delivery, table =>
        {
            table.HasCheckConstraint("ck_delivery_region_postal_range", "postal_code_from <= postal_code_to");
            table.HasCheckConstraint("ck_delivery_region_values", "fee >= 0 and estimated_minutes between 1 and 1440");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(x => x.PostalCodeFrom).HasColumnName("postal_code_from").HasMaxLength(8);
        builder.Property(x => x.PostalCodeTo).HasColumnName("postal_code_to").HasMaxLength(8);
        builder.Property(x => x.Fee).HasColumnName("fee").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
        builder.Property(x => x.EstimatedMinutes).HasColumnName("estimated_minutes");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.IsActive, x.PostalCodeFrom, x.PostalCodeTo });
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
