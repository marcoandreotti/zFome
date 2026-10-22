using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplateRecord>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateRecord> builder)
    {
        builder.ToTable("notification_template", DatabaseSchemas.Communications);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(100);
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Language).HasColumnName("language").HasMaxLength(16);
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(250);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(10000);
        builder.Property(x => x.ProviderTemplateName).HasColumnName("provider_template_name").HasMaxLength(200);
        builder.Property(x => x.RequiresConsent).HasColumnName("requires_consent");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Channel, x.Purpose, x.Language }).IsUnique();
        builder.HasOne<ZFome.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationConsentConfiguration : IEntityTypeConfiguration<NotificationConsentRecord>
{
    public void Configure(EntityTypeBuilder<NotificationConsentRecord> builder)
    {
        builder.ToTable("notification_consent", DatabaseSchemas.Communications);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(100);
        builder.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(254);
        builder.Property(x => x.NormalizedDestination).HasColumnName("normalized_destination").HasMaxLength(254);
        builder.Property(x => x.IsGranted).HasColumnName("is_granted");
        builder.Property(x => x.CapturedAtUtc).HasColumnName("captured_at_utc");
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(200);
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Channel, x.Purpose, x.NormalizedDestination, x.CapturedAtUtc });
        builder.HasOne<ZFome.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notification", DatabaseSchemas.Communications);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(100);
        builder.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(254);
        builder.Property(x => x.NormalizedDestination).HasColumnName("normalized_destination").HasMaxLength(254);
        builder.Property(x => x.Language).HasColumnName("language").HasMaxLength(16);
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(250);
        builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(10000);
        builder.Property(x => x.ProviderTemplateName).HasColumnName("provider_template_name").HasMaxLength(200);
        builder.Property(x => x.RequiresConsent).HasColumnName("requires_consent");
        builder.Property(x => x.ParametersJson).HasColumnName("parameters_json").HasColumnType("jsonb");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(200);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(200);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.CreatedAtUtc });
        builder.HasOne<ZFome.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NotificationTemplateRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.TemplateId })
            .HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("ck_communication_notification_attempt_count", "attempt_count >= 0"));
    }
}

internal sealed class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttemptRecord>
{
    public void Configure(EntityTypeBuilder<NotificationAttemptRecord> builder)
    {
        builder.ToTable("notification_attempt", DatabaseSchemas.Communications);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.NotificationId).HasColumnName("notification_id");
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(200);
        builder.Property(x => x.SafeErrorCode).HasColumnName("safe_error_code").HasMaxLength(200);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.NotificationId, x.AttemptNumber }).IsUnique();
        builder.HasOne<NotificationRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.NotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(table => table.HasCheckConstraint("ck_communication_notification_attempt_number", "attempt_number > 0"));
    }
}
