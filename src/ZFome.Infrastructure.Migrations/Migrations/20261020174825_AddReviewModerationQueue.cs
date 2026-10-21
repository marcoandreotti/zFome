using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModerationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_review_moderation_action",
                schema: "reviews",
                table: "review_moderation_history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_status",
                schema: "reviews",
                table: "review");
        }
    }
}
