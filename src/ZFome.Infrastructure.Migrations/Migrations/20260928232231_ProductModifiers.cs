using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ProductModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pricing_strategy",
                schema: "catalog",
                table: "additional_group",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Additive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pricing_strategy",
                schema: "catalog",
                table: "additional_group");
        }
    }
}
