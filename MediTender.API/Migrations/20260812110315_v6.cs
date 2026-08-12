using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediTender.API.Migrations
{
    /// <inheritdoc />
    public partial class v6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedVerificationAttempts",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedVerificationAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
