using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AdministrativeAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_code",
                schema: "tenancy",
                table: "tenant",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE tenancy.tenant SET public_code = 'TEN-' || UPPER(SUBSTRING(REPLACE(id::text, '-', ''), 1, 8)) WHERE public_code = '';");

            migrationBuilder.CreateTable(
                name: "administrative_session",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_type = table.Column<short>(type: "smallint", nullable: false),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    csrf_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_change_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    access_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    refresh_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_session", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "authentication_challenge",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_type = table.Column<short>(type: "smallint", nullable: false),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    origin_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_challenge", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_user",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    password_change_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_user", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_public_code",
                schema: "tenancy",
                table: "tenant",
                column: "public_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_session_access_token_hash",
                schema: "identity",
                table: "administrative_session",
                column: "access_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_administrative_session_family_id",
                schema: "identity",
                table: "administrative_session",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_administrative_session_refresh_token_hash",
                schema: "identity",
                table: "administrative_session",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_authentication_challenge_origin_hash_created_at",
                schema: "identity",
                table: "authentication_challenge",
                columns: new[] { "origin_hash", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_user_normalized_email",
                schema: "identity",
                table: "platform_user",
                column: "normalized_email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "administrative_session",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "authentication_challenge",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "platform_user",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ux_tenant_public_code",
                schema: "tenancy",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "public_code",
                schema: "tenancy",
                table: "tenant");
        }
    }
}
