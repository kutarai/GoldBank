using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddAccountSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "signature_image",
                schema: "bank",
                table: "accounts",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_verified_by",
                schema: "bank",
                table: "accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "signature_verified_at",
                schema: "bank",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "signature_image",
                schema: "bank",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "signature_verified_by",
                schema: "bank",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "signature_verified_at",
                schema: "bank",
                table: "accounts");
        }
    }
}
