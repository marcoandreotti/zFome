using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageRecord>
{
    public void Configure(EntityTypeBuilder<OutboxMessageRecord> builder)
    {
        builder.ToTable("outbox_message", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(x => x.LeaseToken).HasColumnName("lease_token");
        builder.Property(x => x.LeaseUntilUtc).HasColumnName("lease_until_utc");
        builder.Property(x => x.LastAttemptAtUtc).HasColumnName("last_attempt_at_utc");
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(x => x.DeadLetteredAtUtc).HasColumnName("dead_lettered_at_utc");
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasIndex(x => new { x.NextAttemptAtUtc, x.CreatedAtUtc })
            .HasFilter("processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL");
        builder.HasIndex(x => x.LeaseUntilUtc)
            .HasFilter("processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL AND lease_token IS NOT NULL");
        builder.HasOne<ZFome.Domain.Tenancy.Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_outbox_message_schema_version", "schema_version > 0");
            table.HasCheckConstraint("ck_outbox_message_attempt_count", "attempt_count >= 0");
        });
    }
}
