using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediTender.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantVendorOfferColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluationScore",
                table: "VendorOffers");

            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "VendorOffers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EvaluationScore",
                table: "VendorOffers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                table: "VendorOffers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
