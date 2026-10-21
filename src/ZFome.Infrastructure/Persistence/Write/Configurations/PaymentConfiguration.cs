using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Payments;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade PaymentMethod, definindo o mapeamento para a tabela "payment_method" no esquema de banco de dados "payments".
/// </summary>
internal sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    { builder.ToTable("payment_method", DatabaseSchemas.Payments); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(30); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); builder.Property(x => x.IsOnline).HasColumnName("is_online"); builder.Property(x => x.AllowsChange).HasColumnName("allows_change"); builder.Property(x => x.IsActive).HasColumnName("is_active"); builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.UpdatedAt).HasColumnName("updated_at"); builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Code }).IsUnique(); builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); }
}

/// <summary>
/// Classe de configuração para a entidade Payment, definindo o mapeamento para a tabela "payment" no esquema de banco de dados "payments".
/// </summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment", DatabaseSchemas.Payments, table => table.HasCheckConstraint("ck_payment_amounts", "amount > 0 and (received_amount is null or received_amount >= amount) and change >= 0")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.OrderId).HasColumnName("order_id"); builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id"); builder.Property(x => x.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(30); builder.Property(x => x.PaymentMethodName).HasColumnName("payment_method_name").HasMaxLength(100); builder.Property(x => x.IsOnline).HasColumnName("is_online");
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.ReceivedAmount).HasColumnName("received_amount").HasPrecision(18, 2).HasConversion(x => x.HasValue ? x.Value.Amount : (decimal?)null, x => x.HasValue ? new Money(x.Value) : (Money?)null); builder.Property(x => x.Change).HasColumnName("change").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(100); builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at"); builder.Property(x => x.FailedAt).HasColumnName("failed_at"); builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Status, x.ConfirmedAt })
            .IncludeProperties(x => new { x.OrderId, x.Amount })
            .HasDatabaseName("ix_payment_reporting");
        builder.HasOne<PaymentMethod>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.PaymentMethodId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict); builder.HasOne<ZFome.Domain.Ordering.Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Classe de configuração para a entidade PaymentIdempotency, definindo o mapeamento para a tabela "payment_idempotency" no esquema de banco de dados "payments".
/// </summary>
internal sealed class PaymentIdempotencyConfiguration : IEntityTypeConfiguration<PaymentIdempotency>
{
    public void Configure(EntityTypeBuilder<PaymentIdempotency> builder)
    { builder.ToTable("payment_idempotency", DatabaseSchemas.Payments); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(100); builder.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64); builder.Property(x => x.PaymentId).HasColumnName("payment_id"); builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Key }).IsUnique(); builder.HasOne<Payment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.PaymentId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict); }
}
