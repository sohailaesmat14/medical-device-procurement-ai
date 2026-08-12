using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediTender.API.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfferEvaluations_TenderId_VendorName",
                table: "OfferEvaluations");

            migrationBuilder.CreateIndex(
                name: "IX_OfferEvaluations_TenderId_VendorName",
                table: "OfferEvaluations",
                columns: new[] { "TenderId", "VendorName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfferEvaluations_TenderId_VendorName",
                table: "OfferEvaluations");

            migrationBuilder.CreateIndex(
                name: "IX_OfferEvaluations_TenderId_VendorName",
                table: "OfferEvaluations",
                columns: new[] { "TenderId", "VendorName" });
        }
    }
}
