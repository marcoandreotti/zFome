using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedOutboxReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_outbox_consumer_receipt_outbox_message_message_id",
                schema: "integration",
                table: "outbox_consumer_receipt");

            migrationBuilder.DropIndex(
                name: "IX_outbox_consumer_receipt_message_id",
                schema: "integration",
                table: "outbox_consumer_receipt");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_outbox_message_tenant_id_id",
                schema: "integration",
                table: "outbox_message",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "FK_outbox_consumer_receipt_outbox_message_tenant_id_message_id",
                schema: "integration",
                table: "outbox_consumer_receipt",
                columns: new[] { "tenant_id", "message_id" },
                principalSchema: "integration",
                principalTable: "outbox_message",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_outbox_consumer_receipt_outbox_message_tenant_id_message_id",
                schema: "integration",
                table: "outbox_consumer_receipt");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_outbox_message_tenant_id_id",
                schema: "integration",
                table: "outbox_message");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_consumer_receipt_message_id",
                schema: "integration",
                table: "outbox_consumer_receipt",
                column: "message_id");

            migrationBuilder.AddForeignKey(
                name: "FK_outbox_consumer_receipt_outbox_message_message_id",
                schema: "integration",
                table: "outbox_consumer_receipt",
                column: "message_id",
                principalSchema: "integration",
                principalTable: "outbox_message",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
