using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integration");

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "integration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dead_lettered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                    table.CheckConstraint("ck_outbox_message_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_outbox_message_schema_version", "schema_version > 0");
                    table.ForeignKey(
                        name: "FK_outbox_message_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_lease_until_utc",
                schema: "integration",
                table: "outbox_message",
                column: "lease_until_utc",
                filter: "processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL AND lease_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_utc_created_at_utc",
                schema: "integration",
                table: "outbox_message",
                columns: new[] { "next_attempt_at_utc", "created_at_utc" },
                filter: "processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_tenant_id_idempotency_key",
                schema: "integration",
                table: "outbox_message",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "integration");
        }
    }
}
