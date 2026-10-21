using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ModifierOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "base_price",
                schema: "orders",
                table: "order_item",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("update orders.order_item set base_price = unit_price");

            migrationBuilder.CreateTable(
                name: "order_item_modifier_group",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    pricing_strategy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_modifier_group", x => x.id);
                    table.UniqueConstraint("AK_order_item_modifier_group_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.ForeignKey(
                        name: "FK_order_item_modifier_group_order_item_tenant_id_establishmen~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_item_id },
                        principalSchema: "orders",
                        principalTable: "order_item",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_item_modifier_option",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    portion_numerator = table.Column<int>(type: "integer", nullable: true),
                    portion_denominator = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_modifier_option", x => x.id);
                    table.CheckConstraint("ck_order_item_modifier_option_portion", "(portion_numerator is null and portion_denominator is null) or (portion_numerator > 0 and portion_denominator > 0 and portion_numerator <= portion_denominator)");
                    table.CheckConstraint("ck_order_item_modifier_option_values", "unit_price >= 0 and quantity > 0");
                    table.ForeignKey(
                        name: "FK_order_item_modifier_option_order_item_modifier_group_tenant~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_item_modifier_group_id },
                        principalSchema: "orders",
                        principalTable: "order_item_modifier_group",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_item_modifier_group_tenant_id",
                schema: "orders",
                table: "order_item_modifier_group",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_modifier_group_tenant_id_establishment_id_order_~",
                schema: "orders",
                table: "order_item_modifier_group",
                columns: new[] { "tenant_id", "establishment_id", "order_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_order_item_modifier_option_tenant_id",
                schema: "orders",
                table: "order_item_modifier_option",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_modifier_option_tenant_id_establishment_id_order~",
                schema: "orders",
                table: "order_item_modifier_option",
                columns: new[] { "tenant_id", "establishment_id", "order_item_modifier_group_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_item_modifier_option",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order_item_modifier_group",
                schema: "orders");

            migrationBuilder.DropColumn(
                name: "base_price",
                schema: "orders",
                table: "order_item");
        }
    }
}
