using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductCatalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProcessedAtAndProductConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "LogTailingOutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogTailingOutboxMessages_Unprocessed",
                table: "LogTailingOutboxMessages",
                column: "CreationDate",
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogTailingOutboxMessages_Unprocessed",
                table: "LogTailingOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "LogTailingOutboxMessages");
        }
    }
}
