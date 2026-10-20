using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderHub.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewRejectionAndTargetIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_review_moderation_action",
                schema: "reviews",
                table: "review_moderation_history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_status",
                schema: "reviews",
                table: "review");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_moderation_action",
                schema: "reviews",
                table: "review_moderation_history",
                sql: "action in ('Approved', 'Rejected', 'Hidden', 'Restored')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_status",
                schema: "reviews",
                table: "review",
                sql: "status in ('Pending', 'Published', 'Hidden', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rating_summary_target",
                schema: "reviews",
                table: "rating_summary",
                sql: "(target_kind = 'Establishment' and target_id = establishment_id) or target_kind = 'Product'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $review_rejection_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM reviews.review WHERE status = 'Rejected')
                       OR EXISTS (SELECT 1 FROM reviews.review_moderation_history WHERE action = 'Rejected') THEN
                        RAISE EXCEPTION 'Cannot remove review rejection support while rejected reviews exist.';
                    END IF;
                END
                $review_rejection_rollback$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_moderation_action",
                schema: "reviews",
                table: "review_moderation_history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_status",
                schema: "reviews",
                table: "review");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rating_summary_target",
                schema: "reviews",
                table: "rating_summary");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_moderation_action",
                schema: "reviews",
                table: "review_moderation_history",
                sql: "action in ('Approved', 'Hidden', 'Restored')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_status",
                schema: "reviews",
                table: "review",
                sql: "status in ('Pending', 'Published', 'Hidden')");
        }
    }
}
