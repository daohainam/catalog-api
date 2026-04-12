using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductCatalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ProductHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ProductData = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Variants_IsActive",
                table: "Variants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Variants_Sku",
                table: "Variants",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_VariantDimensionValues_VariantId",
                table: "VariantDimensionValues",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_UrlSlug",
                table: "Products",
                column: "UrlSlug");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDimensions_ProductId",
                table: "ProductDimensions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UrlSlug",
                table: "Categories",
                column: "UrlSlug");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_UrlSlug",
                table: "Brands",
                column: "UrlSlug");

            migrationBuilder.CreateIndex(
                name: "IX_ProductHistories_ProductId",
                table: "ProductHistories",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductHistories_ProductId_Version",
                table: "ProductHistories",
                columns: new[] { "ProductId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductHistories");

            migrationBuilder.DropIndex(
                name: "IX_Variants_IsActive",
                table: "Variants");

            migrationBuilder.DropIndex(
                name: "IX_Variants_Sku",
                table: "Variants");

            migrationBuilder.DropIndex(
                name: "IX_VariantDimensionValues_VariantId",
                table: "VariantDimensionValues");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_UrlSlug",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductDimensions_ProductId",
                table: "ProductDimensions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UrlSlug",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Brands_UrlSlug",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");
        }
    }
}
