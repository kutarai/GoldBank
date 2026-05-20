using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using GoldBank.Core.Common.Persistence;

#nullable disable

namespace GoldBank.Migrator.Migrations.GoldBankDb
{
    /// <inheritdoc />
    [DbContext(typeof(GoldBankDbContext))]
    [Migration("20260430120000_AddLoanCollateralAssetIds")]
    public partial class AddLoanCollateralAssetIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "collateral_asset_ids",
                schema: "bank",
                table: "loans",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collateral_asset_ids",
                schema: "bank",
                table: "loans");
        }
    }
}
