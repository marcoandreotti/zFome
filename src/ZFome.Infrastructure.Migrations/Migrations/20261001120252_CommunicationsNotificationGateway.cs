using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationsNotificationGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communications");

            migrationBuilder.CreateTable(
                name: "notification_consent",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_granted = table.Column<bool>(type: "boolean", nullable: false),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_consent", x => x.id);
                    table.UniqueConstraint("AK_notification_consent_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.ForeignKey(
                        name: "FK_notification_consent_establishment_tenant_id_establishment_~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_consent_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_template",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    provider_template_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requires_consent = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_template", x => x.id);
                    table.UniqueConstraint("AK_notification_template_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.ForeignKey(
                        name: "FK_notification_template_establishment_tenant_id_establishment~",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_template_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    provider_template_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requires_consent = table.Column<bool>(type: "boolean", nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.id);
                    table.UniqueConstraint("AK_notification_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_communication_notification_attempt_count", "attempt_count >= 0");
                    table.ForeignKey(
                        name: "FK_notification_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_notification_template_tenant_id_establishment_~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.template_id },
                        principalSchema: "communications",
                        principalTable: "notification_template",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_attempt",
                schema: "communications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    safe_error_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_attempt", x => x.id);
                    table.CheckConstraint("ck_communication_notification_attempt_number", "attempt_number > 0");
                    table.ForeignKey(
                        name: "FK_notification_attempt_notification_tenant_id_establishment_i~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.notification_id },
                        principalSchema: "communications",
                        principalTable: "notification",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_tenant_id_establishment_id_created_at_utc",
                schema: "communications",
                table: "notification",
                columns: new[] { "tenant_id", "establishment_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_tenant_id_establishment_id_idempotency_key",
                schema: "communications",
                table: "notification",
                columns: new[] { "tenant_id", "establishment_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_tenant_id_establishment_id_template_id",
                schema: "communications",
                table: "notification",
                columns: new[] { "tenant_id", "establishment_id", "template_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_attempt_tenant_id_establishment_id_notificatio~",
                schema: "communications",
                table: "notification_attempt",
                columns: new[] { "tenant_id", "establishment_id", "notification_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_consent_tenant_id_establishment_id_channel_pur~",
                schema: "communications",
                table: "notification_consent",
                columns: new[] { "tenant_id", "establishment_id", "channel", "purpose", "normalized_destination" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_template_tenant_id_establishment_id_channel_pu~",
                schema: "communications",
                table: "notification_template",
                columns: new[] { "tenant_id", "establishment_id", "channel", "purpose", "language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_attempt",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "notification_consent",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "notification",
                schema: "communications");

            migrationBuilder.DropTable(
                name: "notification_template",
                schema: "communications");
        }
    }
}
