using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class OrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "order",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: true),
                    public_reference = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    service_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    customer_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    delivery_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    delivery_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    delivery_neighborhood = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    delivery_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    delivery_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    delivery_postal_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order", x => x.id);
                    table.UniqueConstraint("AK_order_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_order_number", "number is null or number > 0");
                    table.CheckConstraint("ck_order_totals", "subtotal >= 0 and discount >= 0 and fees >= 0 and total >= 0");
                    table.ForeignKey(
                        name: "FK_order_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_number_counter",
                schema: "orders",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_number_counter", x => new { x.tenant_id, x.establishment_id });
                    table.CheckConstraint("ck_order_number_counter", "last_number > 0");
                    table.ForeignKey(
                        name: "FK_order_number_counter_establishment_tenant_id_establishment_~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_item",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    variation_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item", x => x.id);
                    table.UniqueConstraint("AK_order_item_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_order_item_values", "unit_price >= 0 and quantity > 0 and total >= 0");
                    table.ForeignKey(
                        name: "FK_order_item_order_tenant_id_establishment_id_order_id",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_id },
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_status_history_order_tenant_id_establishment_id_order~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_id },
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_item_additional",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    additional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_additional", x => x.id);
                    table.CheckConstraint("ck_order_item_additional_values", "unit_price >= 0 and quantity > 0");
                    table.ForeignKey(
                        name: "FK_order_item_additional_order_item_tenant_id_establishment_id~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_item_id },
                        principalSchema: "orders",
                        principalTable: "order_item",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_public_reference",
                schema: "orders",
                table: "order",
                column: "public_reference",
                unique: true,
                filter: "public_reference is not null");

            migrationBuilder.CreateIndex(
                name: "IX_order_tenant_id",
                schema: "orders",
                table: "order",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_tenant_id_establishment_id_number",
                schema: "orders",
                table: "order",
                columns: new[] { "tenant_id", "establishment_id", "number" },
                unique: true,
                filter: "number is not null");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_tenant_id",
                schema: "orders",
                table: "order_item",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_tenant_id_establishment_id_order_id",
                schema: "orders",
                table: "order_item",
                columns: new[] { "tenant_id", "establishment_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_order_item_additional_tenant_id",
                schema: "orders",
                table: "order_item_additional",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_additional_tenant_id_establishment_id_order_item~",
                schema: "orders",
                table: "order_item_additional",
                columns: new[] { "tenant_id", "establishment_id", "order_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_tenant_id",
                schema: "orders",
                table: "order_status_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_history_tenant_id_establishment_id_order_id_oc~",
                schema: "orders",
                table: "order_status_history",
                columns: new[] { "tenant_id", "establishment_id", "order_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_item_additional",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order_number_counter",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order_status_history",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order_item",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "order",
                schema: "orders");
        }
    }
}
