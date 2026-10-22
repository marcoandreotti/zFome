using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class OrderPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "payment_method",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_online = table.Column<bool>(type: "boolean", nullable: false),
                    allows_change = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_method", x => x.id);
                    table.UniqueConstraint("AK_payment_method_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.ForeignKey(
                        name: "FK_payment_method_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    payment_method_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_online = table.Column<bool>(type: "boolean", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    received_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    change = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment", x => x.id);
                    table.UniqueConstraint("AK_payment_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_payment_amounts", "amount > 0 and (received_amount is null or received_amount >= amount) and change >= 0");
                    table.ForeignKey(
                        name: "FK_payment_order_tenant_id_establishment_id_order_id",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_id },
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_payment_method_tenant_id_establishment_id_payment_m~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.payment_method_id },
                        principalSchema: "payments",
                        principalTable: "payment_method",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_idempotency",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_idempotency", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_idempotency_payment_tenant_id_establishment_id_paym~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.payment_id },
                        principalSchema: "payments",
                        principalTable: "payment",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_tenant_id",
                schema: "payments",
                table: "payment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_tenant_id_establishment_id_order_id_status",
                schema: "payments",
                table: "payment",
                columns: new[] { "tenant_id", "establishment_id", "order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_tenant_id_establishment_id_payment_method_id",
                schema: "payments",
                table: "payment",
                columns: new[] { "tenant_id", "establishment_id", "payment_method_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_idempotency_tenant_id",
                schema: "payments",
                table: "payment_idempotency",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_idempotency_tenant_id_establishment_id_key",
                schema: "payments",
                table: "payment_idempotency",
                columns: new[] { "tenant_id", "establishment_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_idempotency_tenant_id_establishment_id_payment_id",
                schema: "payments",
                table: "payment_idempotency",
                columns: new[] { "tenant_id", "establishment_id", "payment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_tenant_id",
                schema: "payments",
                table: "payment_method",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_method_tenant_id_establishment_id_code",
                schema: "payments",
                table: "payment_method",
                columns: new[] { "tenant_id", "establishment_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_idempotency",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payment",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payment_method",
                schema: "payments");
        }
    }
}
