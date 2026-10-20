using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderHub.Domain.Reviews;

namespace OrderHub.Infrastructure.Persistence.Write.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("review", DatabaseSchemas.Reviews, table =>
        {
            table.HasCheckConstraint("ck_review_rating", "rating between 1 and 5");
            table.HasCheckConstraint("ck_review_status", "status in ('Pending', 'Published', 'Hidden', 'Rejected')");
            table.HasCheckConstraint("ck_review_target", "(target_kind = 'Establishment' and target_id = establishment_id) or target_kind = 'Product'");
            table.HasCheckConstraint("ck_review_comment", "(rating >= 3) or (comment is not null and length(trim(comment)) between 10 and 1000)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.TargetKind).HasColumnName("target_kind").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TargetId).HasColumnName("target_id");
        builder.Property(x => x.Rating).HasColumnName("rating").HasConversion(value => value.Value, value => Rating.Create(value));
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.ModerationReason).HasColumnName("moderation_reason").HasMaxLength(500);
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderId, x.TargetKind, x.TargetId })
            .IsUnique().HasDatabaseName("ux_review_order_target");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.CreatedAt, x.Id })
            .HasFilter("status = 'Published'").HasDatabaseName("ix_review_public_published");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Status, x.CreatedAt, x.Id })
            .HasDatabaseName("ix_review_admin_status_created");
        builder.HasOne<OrderHub.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderHub.Domain.Ordering.Order>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReviewRequestConfiguration : IEntityTypeConfiguration<ReviewRequest>
{
    public void Configure(EntityTypeBuilder<ReviewRequest> builder)
    {
        builder.ToTable("review_request", DatabaseSchemas.Reviews,
            table => table.HasCheckConstraint("ck_review_request_payload_hash", "length(payload_hash) = 64"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(100);
        builder.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64);
        builder.Property(x => x.ResultJson).HasColumnName("result_json").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Key }).IsUnique()
            .HasDatabaseName("ux_review_request_scope_key");
        builder.HasOne<OrderHub.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReviewModerationHistoryConfiguration : IEntityTypeConfiguration<ReviewModerationHistory>
{
    public void Configure(EntityTypeBuilder<ReviewModerationHistory> builder)
    {
        builder.ToTable("review_moderation_history", DatabaseSchemas.Reviews, table =>
            table.HasCheckConstraint("ck_review_moderation_action", "action in ('Approved', 'Rejected', 'Hidden', 'Restored')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.ReviewId).HasColumnName("review_id");
        builder.Property(x => x.ActorId).HasColumnName("actor_id");
        builder.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.ReviewId, x.OccurredAt, x.Id })
            .HasDatabaseName("ix_review_moderation_review_time");
        builder.HasOne<Review>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.ReviewId })
            .HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReviewRatingSummaryRecord
{
    public Guid TenantId { get; set; }
    public Guid EstablishmentId { get; set; }
    public ReviewTargetKind TargetKind { get; set; }
    public Guid TargetId { get; set; }
    public long Count { get; set; }
    public long RatingSum { get; set; }
    public long Rating1Count { get; set; }
    public long Rating2Count { get; set; }
    public long Rating3Count { get; set; }
    public long Rating4Count { get; set; }
    public long Rating5Count { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ReviewRatingSummaryConfiguration : IEntityTypeConfiguration<ReviewRatingSummaryRecord>
{
    public void Configure(EntityTypeBuilder<ReviewRatingSummaryRecord> builder)
    {
        builder.ToTable("rating_summary", DatabaseSchemas.Reviews, table =>
        {
            table.HasCheckConstraint("ck_rating_summary_counts", "count >= 0 and rating_sum >= 0 and rating_1_count >= 0 and rating_2_count >= 0 and rating_3_count >= 0 and rating_4_count >= 0 and rating_5_count >= 0 and count = rating_1_count + rating_2_count + rating_3_count + rating_4_count + rating_5_count and rating_sum = rating_1_count + 2 * rating_2_count + 3 * rating_3_count + 4 * rating_4_count + 5 * rating_5_count");
            table.HasCheckConstraint("ck_rating_summary_target", "(target_kind = 'Establishment' and target_id = establishment_id) or target_kind = 'Product'");
        });
        builder.HasKey(x => new { x.TenantId, x.EstablishmentId, x.TargetKind, x.TargetId });
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.TargetKind).HasColumnName("target_kind").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TargetId).HasColumnName("target_id");
        builder.Property(x => x.Count).HasColumnName("count");
        builder.Property(x => x.RatingSum).HasColumnName("rating_sum");
        builder.Property(x => x.Rating1Count).HasColumnName("rating_1_count");
        builder.Property(x => x.Rating2Count).HasColumnName("rating_2_count");
        builder.Property(x => x.Rating3Count).HasColumnName("rating_3_count");
        builder.Property(x => x.Rating4Count).HasColumnName("rating_4_count");
        builder.Property(x => x.Rating5Count).HasColumnName("rating_5_count");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<OrderHub.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

