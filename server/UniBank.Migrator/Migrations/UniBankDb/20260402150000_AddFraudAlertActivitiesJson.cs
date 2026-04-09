using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddFraudAlertActivitiesJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "activities_json",
                schema: "bank",
                table: "fraud_alerts",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "activities_json",
                schema: "bank",
                table: "fraud_alerts");
        }
    }
}
