using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationsConsentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_consent_tenant_id_establishment_id_channel_pur~",
                schema: "communications",
                table: "notification_consent");

            migrationBuilder.CreateIndex(
                name: "IX_notification_consent_tenant_id_establishment_id_channel_pur~",
                schema: "communications",
                table: "notification_consent",
                columns: new[] { "tenant_id", "establishment_id", "channel", "purpose", "normalized_destination", "captured_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_consent_tenant_id_establishment_id_channel_pur~",
                schema: "communications",
                table: "notification_consent");

            migrationBuilder.CreateIndex(
                name: "IX_notification_consent_tenant_id_establishment_id_channel_pur~",
                schema: "communications",
                table: "notification_consent",
                columns: new[] { "tenant_id", "establishment_id", "channel", "purpose", "normalized_destination" },
                unique: true);
        }
    }
}
