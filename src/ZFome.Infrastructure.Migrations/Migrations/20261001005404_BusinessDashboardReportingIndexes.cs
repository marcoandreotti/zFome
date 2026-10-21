using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class BusinessDashboardReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payment_reporting",
                schema: "payments",
                table: "payment",
                columns: new[] { "tenant_id", "establishment_id", "status", "confirmed_at" })
                .Annotation("Npgsql:IndexInclude", new[] { "order_id", "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_order_status_history_reporting",
                schema: "orders",
                table: "order_status_history",
                columns: new[] { "tenant_id", "establishment_id", "new_status", "occurred_at" })
                .Annotation("Npgsql:IndexInclude", new[] { "order_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payment_reporting",
                schema: "payments",
                table: "payment");

            migrationBuilder.DropIndex(
                name: "ix_order_status_history_reporting",
                schema: "orders",
                table: "order_status_history");
        }
    }
}
