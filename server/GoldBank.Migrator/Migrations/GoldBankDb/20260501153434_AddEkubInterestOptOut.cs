using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.GoldBankDb
{
    /// <inheritdoc />
    public partial class AddEkubInterestOptOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "apply_interest_on_contributions",
                schema: "bank",
                table: "ekub_groups",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "apply_interest_on_contributions",
                schema: "bank",
                table: "ekub_groups");
        }
    }
}
