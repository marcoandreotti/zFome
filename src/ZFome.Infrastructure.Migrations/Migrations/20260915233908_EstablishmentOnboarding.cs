using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class EstablishmentOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_service_table_tenant_id_establishment_id",
                schema: "operations",
                table: "service_table");

            migrationBuilder.AddColumn<Guid>(
                name: "creation_intent",
                schema: "operations",
                table: "service_table",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "onboarding_completed_at",
                schema: "tenancy",
                table: "establishment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_table_tenant_id_establishment_id_creation_intent",
                schema: "operations",
                table: "service_table",
                columns: new[] { "tenant_id", "establishment_id", "creation_intent" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_service_table_tenant_id_establishment_id_creation_intent",
                schema: "operations",
                table: "service_table");

            migrationBuilder.DropColumn(
                name: "creation_intent",
                schema: "operations",
                table: "service_table");

            migrationBuilder.DropColumn(
                name: "onboarding_completed_at",
                schema: "tenancy",
                table: "establishment");

            migrationBuilder.CreateIndex(
                name: "IX_service_table_tenant_id_establishment_id",
                schema: "operations",
                table: "service_table",
                columns: new[] { "tenant_id", "establishment_id" });
        }
    }
}
