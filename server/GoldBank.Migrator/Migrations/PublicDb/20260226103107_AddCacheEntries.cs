using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.PublicDb
{
    /// <inheritdoc />
    public partial class AddCacheEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cache_entries",
                schema: "bank",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cache_entries", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cache_entries_expiry",
                schema: "bank",
                table: "cache_entries",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cache_entries",
                schema: "bank");
        }
    }
}
