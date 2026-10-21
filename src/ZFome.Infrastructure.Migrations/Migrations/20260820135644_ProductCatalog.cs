using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZFome.Infrastructure.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class ProductCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "additional",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional", x => x.id);
                    table.UniqueConstraint("AK_additional_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_additional_price", "price >= 0");
                    table.ForeignKey(
                        name: "FK_additional_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "additional_group",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    minimum_selection = table.Column<int>(type: "integer", nullable: false),
                    maximum_selection = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_group", x => x.id);
                    table.UniqueConstraint("AK_additional_group_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_additional_group_maximum", "maximum_selection >= 1");
                    table.CheckConstraint("ck_additional_group_minimum", "minimum_selection >= 0");
                    table.CheckConstraint("ck_additional_group_range", "minimum_selection <= maximum_selection");
                    table.ForeignKey(
                        name: "FK_additional_group_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                    table.UniqueConstraint("AK_category_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_category_order", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "FK_category_category_tenant_id_establishment_id_parent_categor~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.parent_category_id },
                        principalSchema: "catalog",
                        principalTable: "category",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_category_establishment_tenant_id_establishment_id",
                        columns: x => new { x.tenant_id, x.establishment_id },
                        principalSchema: "tenancy",
                        principalTable: "establishment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "additional_group_item",
                schema: "catalog",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    additional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_group_item", x => new { x.group_id, x.additional_id });
                    table.CheckConstraint("ck_additional_group_item_order", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "FK_additional_group_item_additional_group_tenant_id_establishm~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.group_id },
                        principalSchema: "catalog",
                        principalTable: "additional_group",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_additional_group_item_additional_tenant_id_establishment_id~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.additional_id },
                        principalSchema: "catalog",
                        principalTable: "additional",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    allows_notes = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.id);
                    table.UniqueConstraint("AK_product_tenant_id_establishment_id_id", x => new { x.tenant_id, x.establishment_id, x.id });
                    table.CheckConstraint("ck_product_base_price", "base_price >= 0");
                    table.ForeignKey(
                        name: "FK_product_category_tenant_id_establishment_id_category_id",
                        columns: x => new { x.tenant_id, x.establishment_id, x.category_id },
                        principalSchema: "catalog",
                        principalTable: "category",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_additional_group",
                schema: "catalog",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_additional_group", x => new { x.product_id, x.group_id });
                    table.CheckConstraint("ck_product_additional_group_order", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_additional_group_additional_group_tenant_id_establi~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.group_id },
                        principalSchema: "catalog",
                        principalTable: "additional_group",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_additional_group_product_tenant_id_establishment_id~",
                        columns: x => new { x.tenant_id, x.establishment_id, x.product_id },
                        principalSchema: "catalog",
                        principalTable: "product",
                        principalColumns: new[] { "tenant_id", "establishment_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_image",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_principal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_image", x => x.id);
                    table.CheckConstraint("ck_product_image_order", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_image_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variation",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variation", x => x.id);
                    table.CheckConstraint("ck_product_variation_order", "\"order\" >= 0");
                    table.CheckConstraint("ck_product_variation_price", "price >= 0");
                    table.ForeignKey(
                        name: "FK_product_variation_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_additional_tenant_id",
                schema: "catalog",
                table: "additional",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_additional_tenant_id_establishment_id_name",
                schema: "catalog",
                table: "additional",
                columns: new[] { "tenant_id", "establishment_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_tenant_id",
                schema: "catalog",
                table: "additional_group",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_tenant_id_establishment_id_name",
                schema: "catalog",
                table: "additional_group",
                columns: new[] { "tenant_id", "establishment_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_item_group_id_order",
                schema: "catalog",
                table: "additional_group_item",
                columns: new[] { "group_id", "order" });

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_item_tenant_id_establishment_id_additional~",
                schema: "catalog",
                table: "additional_group_item",
                columns: new[] { "tenant_id", "establishment_id", "additional_id" });

            migrationBuilder.CreateIndex(
                name: "IX_additional_group_item_tenant_id_establishment_id_group_id",
                schema: "catalog",
                table: "additional_group_item",
                columns: new[] { "tenant_id", "establishment_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_category_tenant_id",
                schema: "catalog",
                table: "category",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_category_tenant_id_establishment_id_parent_category_id_order",
                schema: "catalog",
                table: "category",
                columns: new[] { "tenant_id", "establishment_id", "parent_category_id", "order" });

            migrationBuilder.CreateIndex(
                name: "IX_product_tenant_id",
                schema: "catalog",
                table: "product",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_tenant_id_establishment_id_category_id",
                schema: "catalog",
                table: "product",
                columns: new[] { "tenant_id", "establishment_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_tenant_id_establishment_id_code",
                schema: "catalog",
                table: "product",
                columns: new[] { "tenant_id", "establishment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_additional_group_product_id_order",
                schema: "catalog",
                table: "product_additional_group",
                columns: new[] { "product_id", "order" });

            migrationBuilder.CreateIndex(
                name: "IX_product_additional_group_tenant_id_establishment_id_group_id",
                schema: "catalog",
                table: "product_additional_group",
                columns: new[] { "tenant_id", "establishment_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_additional_group_tenant_id_establishment_id_product~",
                schema: "catalog",
                table: "product_additional_group",
                columns: new[] { "tenant_id", "establishment_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_image_product_id",
                schema: "catalog",
                table: "product_image",
                column: "product_id",
                unique: true,
                filter: "is_principal");

            migrationBuilder.CreateIndex(
                name: "IX_product_image_product_id_order",
                schema: "catalog",
                table: "product_image",
                columns: new[] { "product_id", "order" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variation_product_id_order",
                schema: "catalog",
                table: "product_variation",
                columns: new[] { "product_id", "order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "additional_group_item",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_additional_group",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_image",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_variation",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "additional",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "additional_group",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "category",
                schema: "catalog");
        }
    }
}
