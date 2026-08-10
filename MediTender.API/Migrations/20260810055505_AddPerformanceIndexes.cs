using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediTender.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorOffers_TenderId",
                table: "VendorOffers");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "VendorOffers",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "VendorName",
                table: "OfferEvaluations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOffers_TenderId_CompanyName",
                table: "VendorOffers",
                columns: new[] { "TenderId", "CompanyName" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferEvaluations_TenderId_VendorName",
                table: "OfferEvaluations",
                columns: new[] { "TenderId", "VendorName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorOffers_TenderId_CompanyName",
                table: "VendorOffers");

            migrationBuilder.DropIndex(
                name: "IX_OfferEvaluations_TenderId_VendorName",
                table: "OfferEvaluations");

            migrationBuilder.AlterColumn<string>(
                name: "CompanyName",
                table: "VendorOffers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "VendorName",
                table: "OfferEvaluations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOffers_TenderId",
                table: "VendorOffers",
                column: "TenderId");
        }
    }
}
