using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ModifierCompatibilityAndTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_additional_group_item_tenant_id_establishment_id_group_id",
                schema: "catalog",
                table: "additional_group_item");

            migrationBuilder.AddColumn<bool>(
                name: "requires_complete_composition",
                schema: "catalog",
                table: "additional_group",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "catalog",
                table: "additional_group",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Additional");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_additional_group_item_tenant_id_establishment_id_group_id_a~",
                schema: "catalog",
                table: "additional_group_item",
                columns: new[] { "tenant_id", "establishment_id", "group_id", "additional_id" });

            migrationBuilder.CreateTable(
                name: "additional_group_compatibility_rule",
                schema: "catalog",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_additional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_additional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_group_compatibility_rule", x => new { x.tenant_id, x.establishment_id, x.source_group_id, x.source_additional_id, x.target_group_id, x.target_additional_id, x.kind });
                    table.CheckConstraint("ck_additional_group_compatibility_rule_target", "source_group_id <> target_group_id or source_additional_id <> target_additional_id");
                    table.ForeignKey(
                        name: "FK_additional_group_compatibility_rule_additional_group_item_t~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.source_group_id, x.source_additional_id },
                        principalSchema: "catalog",
                        principalTable: "additional_group_item",
                        principalColumns: new[] { "tenant_id", "establishment_id", "group_id", "additional_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_additional_group_compatibility_rule_additional_group_item_~1",
                        columns: x => new { x.tenant_id, x.establishment_id, x.target_group_id, x.target_additional_id },
                        principalSchema: "catalog",
                        principalTable: "additional_group_item",
                        principalColumns: new[] { "tenant_id", "establishment_id", "group_id", "additional_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_additional_group_compatibility_rule_additional_group_tenant~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.source_group_id },
                        principalSchema: "catalog",
                        principalTable: "additional_group",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_compatibility_rule_tenant_id_establishment~",
                schema: "catalog",
                table: "additional_group_compatibility_rule",
                columns: new[] { "tenant_id", "establishment_id", "target_group_id", "target_additional_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "additional_group_compatibility_rule",
                schema: "catalog");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_additional_group_item_tenant_id_establishment_id_group_id_a~",
                schema: "catalog",
                table: "additional_group_item");

            migrationBuilder.DropColumn(
                name: "requires_complete_composition",
                schema: "catalog",
                table: "additional_group");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "catalog",
                table: "additional_group");

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_item_tenant_id_establishment_id_group_id",
                schema: "catalog",
                table: "additional_group_item",
                columns: new[] { "tenant_id", "establishment_id", "group_id" });
        }
    }
}
