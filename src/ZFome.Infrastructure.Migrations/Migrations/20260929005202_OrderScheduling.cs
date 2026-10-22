using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class OrderScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_at_utc",
                schema: "orders",
                table: "order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scheduled_time_zone_id",
                schema: "orders",
                table: "order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "order_scheduling_policy",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<short>(type: "smallint", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    minimum_advance_minutes = table.Column<int>(type: "integer", nullable: false),
                    horizon_days = table.Column<int>(type: "integer", nullable: false),
                    maximum_orders_per_slot = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_scheduling_policy", x => x.id);
                    table.UniqueConstraint("AK_order_scheduling_policy_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_order_scheduling_policy_capacity", "maximum_orders_per_slot is null or maximum_orders_per_slot > 0");
                    table.CheckConstraint("ck_order_scheduling_policy_window", "minimum_advance_minutes >= 0 and horizon_days between 1 and 90 and minimum_advance_minutes <= horizon_days * 1440");
                    table.ForeignKey(
                        name: "FK_order_scheduling_policy_establishment_tenant_id_establishme~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_tenant_id_establishment_id_service_type_scheduled_at_~",
                schema: "orders",
                table: "order",
                columns: new[] { "tenant_id", "establishment_id", "service_type", "scheduled_at_utc" },
                filter: "scheduled_at_utc is not null");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_schedule",
                schema: "orders",
                table: "order",
                sql: "(scheduled_at_utc is null and scheduled_time_zone_id is null) or (scheduled_at_utc is not null and scheduled_time_zone_id is not null)");

            migrationBuilder.CreateIndex(
                name: "IX_order_scheduling_policy_tenant_id",
                schema: "operations",
                table: "order_scheduling_policy",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_scheduling_policy_tenant_id_establishment_id_service_~",
                schema: "operations",
                table: "order_scheduling_policy",
                columns: new[] { "tenant_id", "establishment_id", "service_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_scheduling_policy",
                schema: "operations");

            migrationBuilder.DropIndex(
                name: "IX_order_tenant_id_establishment_id_service_type_scheduled_at_~",
                schema: "orders",
                table: "order");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_schedule",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "scheduled_at_utc",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "scheduled_time_zone_id",
                schema: "orders",
                table: "order");
        }
    }
}
