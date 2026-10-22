using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade ServiceTable, definindo o mapeamento para a tabela "service_table" no esquema "operations" do banco de dados.
/// </summary>
internal sealed class ServiceTableConfiguration : IEntityTypeConfiguration<ServiceTable>
{
    public void Configure(EntityTypeBuilder<ServiceTable> builder)
    {
        builder.ToTable("service_table", DatabaseSchemas.Operations);
        builder.HasKey(table => table.Id); builder.Property(table => table.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(table => table.TenantId).HasColumnName("tenant_id"); builder.Property(table => table.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(table => table.Code).HasColumnName("code").HasMaxLength(30); builder.Property(table => table.Description).HasColumnName("description").HasMaxLength(100);
        builder.Property(table => table.QrCodeToken).HasColumnName("qr_code_token").HasMaxLength(100); builder.Property(table => table.IsActive).HasColumnName("is_active");
        builder.Property(table => table.CreationIntent).HasColumnName("creation_intent");
        builder.HasIndex(table => new { table.TenantId, table.EstablishmentId, table.CreationIntent }).IsUnique();
        builder.HasIndex(table => new { table.EstablishmentId, table.Code }).IsUnique(); builder.HasIndex(table => table.QrCodeToken).IsUnique();
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(table => new { table.TenantId, table.EstablishmentId }).HasPrincipalKey(establishment => new { establishment.TenantId, establishment.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Classe de configuração para a entidade BusinessHours, definindo o mapeamento para a tabela "business_hours" no esquema "operations" do banco de dados.
/// </summary>
internal sealed class BusinessHoursConfiguration : IEntityTypeConfiguration<BusinessHours>
{
    public void Configure(EntityTypeBuilder<BusinessHours> builder)
    {
        builder.ToTable("business_hours", DatabaseSchemas.Operations);
        builder.HasKey(hours => hours.Id); builder.Property(hours => hours.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(hours => hours.TenantId).HasColumnName("tenant_id"); builder.Property(hours => hours.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(hours => hours.DayOfWeek).HasColumnName("day_of_week").HasConversion<short>(); builder.Property(hours => hours.OpensAt).HasColumnName("opens_at").HasColumnType("time without time zone"); builder.Property(hours => hours.ClosesAt).HasColumnName("closes_at").HasColumnType("time without time zone"); builder.Property(hours => hours.IsActive).HasColumnName("is_active");
        builder.ToTable(table => table.HasCheckConstraint("ck_business_hours_interval", "closes_at <> opens_at"));
        builder.HasIndex(hours => new { hours.EstablishmentId, hours.DayOfWeek });
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(hours => new { hours.TenantId, hours.EstablishmentId }).HasPrincipalKey(establishment => new { establishment.TenantId, establishment.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ServiceScheduleExceptionConfiguration : IEntityTypeConfiguration<ServiceScheduleException>
{
    public void Configure(EntityTypeBuilder<ServiceScheduleException> builder)
    {
        builder.ToTable("service_schedule_exception", DatabaseSchemas.Operations, table =>
            table.HasCheckConstraint("ck_service_schedule_exception_interval",
                "(is_open and opens_at is not null and closes_at is not null and opens_at <> closes_at) or (not is_open and opens_at is null and closes_at is null)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Date).HasColumnName("date").HasColumnType("date");
        builder.Property(x => x.ServiceType).HasColumnName("service_type").HasConversion<short?>();
        builder.Property(x => x.IsOpen).HasColumnName("is_open");
        builder.Property(x => x.OpensAt).HasColumnName("opens_at").HasColumnType("time without time zone");
        builder.Property(x => x.ClosesAt).HasColumnName("closes_at").HasColumnType("time without time zone");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(250);
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Date, x.ServiceType }).IsUnique().HasFilter("service_type is not null");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Date }).IsUnique().HasFilter("service_type is null");
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ServicePauseConfiguration : IEntityTypeConfiguration<ServicePause>
{
    public void Configure(EntityTypeBuilder<ServicePause> builder)
    {
        builder.ToTable("service_pause", DatabaseSchemas.Operations, table =>
            table.HasCheckConstraint("ck_service_pause_interval", "ends_at is null or ends_at > starts_at"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.ServiceType).HasColumnName("service_type").HasConversion<short>();
        builder.Property(x => x.StartsAt).HasColumnName("starts_at");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(250);
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.ServiceType, x.StartsAt });
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderSchedulingPolicyConfiguration : IEntityTypeConfiguration<OrderSchedulingPolicy>
{
    public void Configure(EntityTypeBuilder<OrderSchedulingPolicy> builder)
    {
        builder.ToTable("order_scheduling_policy", DatabaseSchemas.Operations, table =>
        {
            table.HasCheckConstraint("ck_order_scheduling_policy_window", "minimum_advance_minutes >= 0 and horizon_days between 1 and 90 and minimum_advance_minutes <= horizon_days * 1440");
            table.HasCheckConstraint("ck_order_scheduling_policy_capacity", "maximum_orders_per_slot is null or maximum_orders_per_slot > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.ServiceType).HasColumnName("service_type").HasConversion<short>();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.MinimumAdvanceMinutes).HasColumnName("minimum_advance_minutes");
        builder.Property(x => x.HorizonDays).HasColumnName("horizon_days");
        builder.Property(x => x.MaximumOrdersPerSlot).HasColumnName("maximum_orders_per_slot");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.ServiceType }).IsUnique();
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
