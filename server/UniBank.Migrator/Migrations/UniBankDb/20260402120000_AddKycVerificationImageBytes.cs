using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddKycVerificationImageBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "id_document_image_data",
                schema: "bank",
                table: "kyc_verifications",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "selfie_image_data",
                schema: "bank",
                table: "kyc_verifications",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "id_document_image_data",
                schema: "bank",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "selfie_image_data",
                schema: "bank",
                table: "kyc_verifications");
        }
    }
}
