using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.GoldBankDb
{
    /// <inheritdoc />
    public partial class AddDisputeActivitiesJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "activities_json",
                schema: "bank",
                table: "disputes",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "activities_json",
                schema: "bank",
                table: "disputes");
        }
    }
}
