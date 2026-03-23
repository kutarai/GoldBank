using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddCardPanAndDualCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_phone_unique",
                schema: "bank",
                table: "accounts");

            migrationBuilder.AddColumn<string>(
                name: "card_pan",
                schema: "bank",
                table: "accounts",
                type: "character varying(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_card_pan_unique",
                schema: "bank",
                table: "accounts",
                column: "card_pan",
                unique: true,
                filter: "card_pan IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_phone_currency_unique",
                schema: "bank",
                table: "accounts",
                columns: new[] { "phone", "currency" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_card_pan_unique",
                schema: "bank",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_phone_currency_unique",
                schema: "bank",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "card_pan",
                schema: "bank",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_phone_unique",
                schema: "bank",
                table: "accounts",
                column: "phone",
                unique: true,
                filter: "deleted_at IS NULL");
        }
    }
}
