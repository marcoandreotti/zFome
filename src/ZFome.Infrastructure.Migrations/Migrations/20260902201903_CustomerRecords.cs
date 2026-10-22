using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.CreateTable(
                name: "customer",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    normalized_phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.id);
                    table.UniqueConstraint("AK_customer_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.ForeignKey(
                        name: "FK_customer_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_address",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    neighborhood = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_address", x => x.id);
                    table.UniqueConstraint("AK_customer_address_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.ForeignKey(
                        name: "FK_customer_address_customer_tenant_id_establishment_id_custom~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.customer_id },
                        principalSchema: "customers",
                        principalTable: "customer",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_tenant_id",
                schema: "customers",
                table: "customer",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_tenant_id_establishment_id_normalized_email",
                schema: "customers",
                table: "customer",
                columns: new[] { "tenant_id", "establishment_id", "normalized_email" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_tenant_id_establishment_id_normalized_phone",
                schema: "customers",
                table: "customer",
                columns: new[] { "tenant_id", "establishment_id", "normalized_phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_address_tenant_id",
                schema: "customers",
                table: "customer_address",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_address_tenant_id_establishment_id_customer_id",
                schema: "customers",
                table: "customer_address",
                columns: new[] { "tenant_id", "establishment_id", "customer_id" },
                unique: true,
                filter: "is_primary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_address",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "customer",
                schema: "customers");
        }
    }
}
