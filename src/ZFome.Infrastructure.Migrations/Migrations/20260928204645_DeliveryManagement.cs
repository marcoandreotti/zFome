using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "delivery");

            migrationBuilder.AddColumn<int>(
                name: "delivery_estimated_minutes",
                schema: "orders",
                table: "order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_fee",
                schema: "orders",
                table: "order",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "delivery_region_id",
                schema: "orders",
                table: "order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_region_name",
                schema: "orders",
                table: "order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delivery_region",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code_from = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    postal_code_to = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_region", x => x.id);
                    table.UniqueConstraint("AK_delivery_region_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_delivery_region_postal_range", "postal_code_from <= postal_code_to");
                    table.CheckConstraint("ck_delivery_region_values", "fee >= 0 and estimated_minutes between 1 and 1440");
                    table.ForeignKey(
                        name: "FK_delivery_region_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_region_tenant_id",
                schema: "delivery",
                table: "delivery_region",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_region_tenant_id_establishment_id_is_active_postal~",
                schema: "delivery",
                table: "delivery_region",
                columns: new[] { "tenant_id", "establishment_id", "is_active", "postal_code_from", "postal_code_to" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_region",
                schema: "delivery");

            migrationBuilder.DropColumn(
                name: "delivery_estimated_minutes",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "delivery_fee",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "delivery_region_id",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "delivery_region_name",
                schema: "orders",
                table: "order");
        }
    }
}
