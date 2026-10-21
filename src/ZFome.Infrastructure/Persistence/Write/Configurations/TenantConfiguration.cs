using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Tenancy;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade Tenant, definindo o mapeamento para o banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenant", DatabaseSchemas.Tenancy);
        builder.HasKey(tenant => tenant.Id).HasName("pk_tenant");
        builder.Property(tenant => tenant.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(tenant => tenant.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(tenant => tenant.PublicCode).HasColumnName("public_code").HasMaxLength(50).IsRequired();
        builder.Property(tenant => tenant.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(tenant => tenant.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(tenant => tenant.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(tenant => tenant.PublicCode).IsUnique().HasDatabaseName("ux_tenant_public_code");
    }
}
