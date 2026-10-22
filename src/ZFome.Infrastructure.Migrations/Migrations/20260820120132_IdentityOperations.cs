using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class IdentityOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_establishment_tenant_id_id",
                schema: "tenancy",
                table: "establishment",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "administrative_role",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "administrative_user",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_access_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_user", x => x.id);
                    table.UniqueConstraint("AK_administrative_user_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_administrative_user_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_hours",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<short>(type: "smallint", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_hours", x => x.id);
                    table.CheckConstraint("ck_business_hours_interval", "closes_at > opens_at");
                    table.ForeignKey(
                        name: "FK_business_hours_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_table",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    qr_code_token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_table", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_table_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "administrative_user_role",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_administrative_user_role_administrative_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "administrative_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_administrative_user_role_administrative_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "administrative_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_establishment_access",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_establishment_access", x => new { x.user_id, x.establishment_id });
                    table.ForeignKey(
                        name: "FK_user_establishment_access_administrative_user_tenant_id_use~",
                        columns: x => new { x.tenant_id, x.user_id },
                        principalSchema: "identity",
                        principalTable: "administrative_user",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_establishment_access_establishment_tenant_id_establish~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "administrative_role",
                columns: new[] { "id", "code", "name" },
                values: new object[,]
                {
                    { (short)1, "OWNER", "Owner" },
                    { (short)2, "ADMIN", "Admin" },
                    { (short)3, "MANAGER", "Manager" },
                    { (short)4, "ATTENDANT", "Attendant" },
                    { (short)5, "KITCHEN", "Kitchen" },
                    { (short)6, "DELIVERY", "Delivery" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_role_code",
                schema: "identity",
                table: "administrative_role",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_user_tenant_id",
                schema: "identity",
                table: "administrative_user",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_administrative_user_tenant_email",
                schema: "identity",
                table: "administrative_user",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_user_role_role_id",
                schema: "identity",
                table: "administrative_user_role",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_hours_establishment_id_day_of_week",
                schema: "operations",
                table: "business_hours",
                columns: new[] { "establishment_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "IX_business_hours_tenant_id",
                schema: "operations",
                table: "business_hours",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_hours_tenant_id_establishment_id",
                schema: "operations",
                table: "business_hours",
                columns: new[] { "tenant_id", "establishment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_table_establishment_id_code",
                schema: "operations",
                table: "service_table",
                columns: new[] { "establishment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_table_qr_code_token",
                schema: "operations",
                table: "service_table",
                column: "qr_code_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_table_tenant_id",
                schema: "operations",
                table: "service_table",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_table_tenant_id_establishment_id",
                schema: "operations",
                table: "service_table",
                columns: new[] { "tenant_id", "establishment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_establishment_access_tenant_id_establishment_id",
                schema: "identity",
                table: "user_establishment_access",
                columns: new[] { "tenant_id", "establishment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_establishment_access_tenant_id_user_id",
                schema: "identity",
                table: "user_establishment_access",
                columns: new[] { "tenant_id", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "administrative_user_role",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "business_hours",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "service_table",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "user_establishment_access",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "administrative_role",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "administrative_user",
                schema: "identity");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_establishment_tenant_id_id",
                schema: "tenancy",
                table: "establishment");
        }
    }
}
