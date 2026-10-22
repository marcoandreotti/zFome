using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class PublicOrderingApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "public_order_request",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_order_request", x => x.id);
                    table.ForeignKey(
                        name: "FK_public_order_request_order_tenant_id_establishment_id_order~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.order_id },
                        principalSchema: "orders",
                        principalTable: "order",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_public_order_request_tenant_id",
                schema: "orders",
                table: "public_order_request",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_public_order_request_tenant_id_establishment_id_key",
                schema: "orders",
                table: "public_order_request",
                columns: new[] { "tenant_id", "establishment_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_order_request_tenant_id_establishment_id_order_id",
                schema: "orders",
                table: "public_order_request",
                columns: new[] { "tenant_id", "establishment_id", "order_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "public_order_request",
                schema: "orders");
        }
    }
}
