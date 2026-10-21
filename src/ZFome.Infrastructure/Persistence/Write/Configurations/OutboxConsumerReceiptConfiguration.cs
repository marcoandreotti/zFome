using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

internal sealed class OutboxConsumerReceiptConfiguration : IEntityTypeConfiguration<OutboxConsumerReceiptRecord>
{
    public void Configure(EntityTypeBuilder<OutboxConsumerReceiptRecord> builder)
    {
        builder.ToTable("outbox_consumer_receipt", DatabaseSchemas.Integration);
        builder.HasKey(x => new { x.TenantId, x.MessageId, x.ConsumerName });
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.MessageId).HasColumnName("message_id");
        builder.Property(x => x.ConsumerName).HasColumnName("consumer_name").HasMaxLength(200);
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.HasOne<OutboxMessageRecord>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.MessageId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
