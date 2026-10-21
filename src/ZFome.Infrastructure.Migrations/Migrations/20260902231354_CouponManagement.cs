using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CouponManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "promotions");

            migrationBuilder.AddColumn<string>(
                name: "coupon_code",
                schema: "orders",
                table: "order",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "coupon_id",
                schema: "orders",
                table: "order",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coupon",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    discount_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_order = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    maximum_uses = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon", x => x.id);
                    table.UniqueConstraint("AK_coupon_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_coupon_uses", "used_count >= 0 and (maximum_uses is null or (maximum_uses > 0 and used_count <= maximum_uses))");
                    table.CheckConstraint("ck_coupon_value", "value > 0 and (discount_type <> 'Percentage' or value <= 100)");
                    table.CheckConstraint("ck_coupon_window", "starts_at < ends_at");
                    table.ForeignKey(
                        name: "FK_coupon_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coupon_use",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon_use", x => x.id);
                    table.CheckConstraint("ck_coupon_use_discount", "discount >= 0");
                    table.ForeignKey(
                        name: "FK_coupon_use_coupon_tenant_id_establishment_id_coupon_id",
                        columns: x => new { x.tenant_id, x.establishment_id, x.coupon_id },
                        principalSchema: "promotions",
                        principalTable: "coupon",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coupon_use_order_tenant_id_establishment_id_order_id",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_id },
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_tenant_id_establishment_id_coupon_id",
                schema: "orders",
                table: "order",
                columns: new[] { "tenant_id", "establishment_id", "coupon_id" });

            migrationBuilder.CreateIndex(
                name: "IX_coupon_tenant_id",
                schema: "promotions",
                table: "coupon",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_tenant_id_establishment_id_code",
                schema: "promotions",
                table: "coupon",
                columns: new[] { "tenant_id", "establishment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coupon_use_tenant_id",
                schema: "promotions",
                table: "coupon_use",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_use_tenant_id_establishment_id_coupon_id_order_id",
                schema: "promotions",
                table: "coupon_use",
                columns: new[] { "tenant_id", "establishment_id", "coupon_id", "order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coupon_use_tenant_id_establishment_id_order_id",
                schema: "promotions",
                table: "coupon_use",
                columns: new[] { "tenant_id", "establishment_id", "order_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_order_coupon_tenant_id_establishment_id_coupon_id",
                schema: "orders",
                table: "order",
                columns: new[] { "tenant_id", "establishment_id", "coupon_id" },
                principalSchema: "promotions",
                principalTable: "coupon",
                principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_coupon_tenant_id_establishment_id_coupon_id",
                schema: "orders",
                table: "order");

            migrationBuilder.DropTable(
                name: "coupon_use",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "coupon",
                schema: "promotions");

            migrationBuilder.DropIndex(
                name: "IX_order_tenant_id_establishment_id_coupon_id",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "coupon_code",
                schema: "orders",
                table: "order");

            migrationBuilder.DropColumn(
                name: "coupon_id",
                schema: "orders",
                table: "order");
        }
    }
}
