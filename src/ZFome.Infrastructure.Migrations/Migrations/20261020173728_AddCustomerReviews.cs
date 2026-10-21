using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reviews");

            migrationBuilder.CreateTable(
                name: "rating_summary",
                schema: "reviews",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count = table.Column<long>(type: "bigint", nullable: false),
                    rating_sum = table.Column<long>(type: "bigint", nullable: false),
                    rating_1_count = table.Column<long>(type: "bigint", nullable: false),
                    rating_2_count = table.Column<long>(type: "bigint", nullable: false),
                    rating_3_count = table.Column<long>(type: "bigint", nullable: false),
                    rating_4_count = table.Column<long>(type: "bigint", nullable: false),
                    rating_5_count = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rating_summary", x => new { x.tenant_id, x.establishment_id, x.target_kind, x.target_id });
                    table.CheckConstraint("ck_rating_summary_counts", "count >= 0 and rating_sum >= 0 and rating_1_count >= 0 and rating_2_count >= 0 and rating_3_count >= 0 and rating_4_count >= 0 and rating_5_count >= 0 and count = rating_1_count + rating_2_count + rating_3_count + rating_4_count + rating_5_count and rating_sum = rating_1_count + 2 * rating_2_count + 3 * rating_3_count + 4 * rating_4_count + 5 * rating_5_count");
                    table.ForeignKey(
                        name: "FK_rating_summary_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review",
                schema: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    moderation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review", x => x.id);
                    table.UniqueConstraint("AK_review_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_review_comment", "(rating >= 3) or (comment is not null and length(trim(comment)) between 10 and 1000)");
                    table.CheckConstraint("ck_review_rating", "rating between 1 and 5");
                    table.CheckConstraint("ck_review_target", "(target_kind = 'Establishment' and target_id = establishment_id) or target_kind = 'Product'");
                    table.ForeignKey(
                        name: "FK_review_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_order_tenant_id_establishment_id_order_id",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_id },
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_request",
                schema: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_request", x => x.id);
                    table.CheckConstraint("ck_review_request_payload_hash", "length(payload_hash) = 64");
                    table.ForeignKey(
                        name: "FK_review_request_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_moderation_history",
                schema: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_moderation_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_review_moderation_history_review_tenant_id_establishment_id~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.review_id },
                        principalSchema: "reviews",
                        principalTable: "review",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_admin_status_created",
                schema: "reviews",
                table: "review",
                columns: new[] { "tenant_id", "establishment_id", "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_review_public_published",
                schema: "reviews",
                table: "review",
                columns: new[] { "tenant_id", "establishment_id", "created_at", "id" },
                filter: "status = 'Published'");

            migrationBuilder.CreateIndex(
                name: "IX_review_tenant_id",
                schema: "reviews",
                table: "review",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_review_order_target",
                schema: "reviews",
                table: "review",
                columns: new[] { "tenant_id", "establishment_id", "order_id", "target_kind", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_moderation_history_tenant_id",
                schema: "reviews",
                table: "review_moderation_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_moderation_review_time",
                schema: "reviews",
                table: "review_moderation_history",
                columns: new[] { "tenant_id", "establishment_id", "review_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_review_request_tenant_id",
                schema: "reviews",
                table: "review_request",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_review_request_scope_key",
                schema: "reviews",
                table: "review_request",
                columns: new[] { "tenant_id", "establishment_id", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $review_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM reviews.review)
                       OR EXISTS (SELECT 1 FROM reviews.review_request)
                       OR EXISTS (SELECT 1 FROM reviews.review_moderation_history)
                       OR EXISTS (SELECT 1 FROM reviews.rating_summary) THEN
                        RAISE EXCEPTION 'Cannot roll back customer reviews while review data exists; disable the feature and retain the schema.';
                    END IF;
                END
                $review_rollback$;
                """);

            migrationBuilder.DropTable(
                name: "rating_summary",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_moderation_history",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review_request",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "review",
                schema: "reviews");
        }
    }
}
