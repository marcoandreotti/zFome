using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class BusinessHoursAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("alter table operations.business_hours drop constraint if exists ck_business_hours_interval;");

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                schema: "tenancy",
                table: "establishment",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "America/Sao_Paulo");

            migrationBuilder.CreateTable(
                name: "offer_unavailability",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_unavailability", x => x.id);
                    table.CheckConstraint("ck_offer_unavailability_interval", "ends_at is null or ends_at > starts_at");
                    table.ForeignKey(
                        name: "FK_offer_unavailability_establishment_tenant_id_establishment_~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_pause",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<short>(type: "smallint", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_pause", x => x.id);
                    table.CheckConstraint("ck_service_pause_interval", "ends_at is null or ends_at > starts_at");
                    table.ForeignKey(
                        name: "FK_service_pause_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_schedule_exception",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    service_type = table.Column<short>(type: "smallint", nullable: true),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_schedule_exception", x => x.id);
                    table.CheckConstraint("ck_service_schedule_exception_interval", "(is_open and opens_at is not null and closes_at is not null and opens_at <> closes_at) or (not is_open and opens_at is null and closes_at is null)");
                    table.ForeignKey(
                        name: "FK_service_schedule_exception_establishment_tenant_id_establis~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_business_hours_interval",
                schema: "operations",
                table: "business_hours",
                sql: "closes_at <> opens_at");

            migrationBuilder.CreateIndex(
                name: "IX_offer_unavailability_tenant_id",
                schema: "catalog",
                table: "offer_unavailability",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_offer_unavailability_tenant_id_establishment_id_kind_offer_~",
                schema: "catalog",
                table: "offer_unavailability",
                columns: new[] { "tenant_id", "establishment_id", "kind", "offer_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "IX_service_pause_tenant_id",
                schema: "operations",
                table: "service_pause",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_pause_tenant_id_establishment_id_service_type_start~",
                schema: "operations",
                table: "service_pause",
                columns: new[] { "tenant_id", "establishment_id", "service_type", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "IX_service_schedule_exception_tenant_id",
                schema: "operations",
                table: "service_schedule_exception",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_schedule_exception_tenant_id_establishment_id_date",
                schema: "operations",
                table: "service_schedule_exception",
                columns: new[] { "tenant_id", "establishment_id", "date" },
                unique: true,
                filter: "service_type is null");

            migrationBuilder.CreateIndex(
                name: "IX_service_schedule_exception_tenant_id_establishment_id_date_~",
                schema: "operations",
                table: "service_schedule_exception",
                columns: new[] { "tenant_id", "establishment_id", "date", "service_type" },
                unique: true,
                filter: "service_type is not null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offer_unavailability",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "service_pause",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "service_schedule_exception",
                schema: "operations");

            migrationBuilder.Sql("alter table operations.business_hours drop constraint if exists ck_business_hours_interval;");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                schema: "tenancy",
                table: "establishment");

            // Preserve overnight intervals on rollback. The previous application version
            // remains compatible with existing same-day rows without restoring the stricter check.
        }
    }
}
