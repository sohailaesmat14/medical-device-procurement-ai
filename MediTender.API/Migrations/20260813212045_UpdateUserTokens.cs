using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediTender.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TokenExpiration",
                table: "Users",
                newName: "VerificationTokenExpiration");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetPasswordTokenExpiration",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetPasswordTokenExpiration",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "VerificationTokenExpiration",
                table: "Users",
                newName: "TokenExpiration");
        }
    }
}
