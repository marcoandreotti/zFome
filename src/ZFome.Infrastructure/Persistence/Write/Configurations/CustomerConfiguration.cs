using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Customers;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade Customer, definindo o mapeamento entre a classe e a tabela do banco de dados, incluindo chaves primárias, propriedades e relacionamentos.
/// </summary>
internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer", DatabaseSchemas.Customers);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(x => x.NormalizedPhone).HasColumnName("normalized_phone").HasMaxLength(15);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(254);
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.NormalizedPhone }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.NormalizedEmail });
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Addresses)
            .WithOne()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.CustomerId })
            .HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Classe de configuração para a entidade CustomerAddress, definindo o mapeamento entre a classe e a tabela do banco de dados, incluindo chaves primárias, propriedades e relacionamentos.
/// </summary>
internal sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_address", DatabaseSchemas.Customers);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(50);
        builder.Property(x => x.Street).HasColumnName("street").HasMaxLength(200);
        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(30);
        builder.Property(x => x.Complement).HasColumnName("complement").HasMaxLength(100);
        builder.Property(x => x.Neighborhood).HasColumnName("neighborhood").HasMaxLength(100);
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(12);
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.CustomerId });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.CustomerId })
            .HasFilter("is_primary")
            .IsUnique();
    }
}