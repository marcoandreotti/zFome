using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade PublicOrderRequest, definindo o mapeamento para a tabela "public_order_request" no esquema de banco de dados "Orders".
/// </summary>
internal sealed class PublicOrderRequestConfiguration : IEntityTypeConfiguration<PublicOrderRequest>
{
    public void Configure(EntityTypeBuilder<PublicOrderRequest> builder)
    {
        builder.ToTable("public_order_request", DatabaseSchemas.Orders); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(100); builder.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64); builder.Property(x => x.OrderId).HasColumnName("order_id"); builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Key }).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}