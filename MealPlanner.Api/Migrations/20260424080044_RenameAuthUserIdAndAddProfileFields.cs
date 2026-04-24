using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameAuthUserIdAndAddProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Auth0UserId",
                table: "users",
                newName: "AuthUserId");

            migrationBuilder.RenameIndex(
                name: "IX_users_Auth0UserId",
                table: "users",
                newName: "IX_users_AuthUserId");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "users");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Timezone",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "AuthUserId",
                table: "users",
                newName: "Auth0UserId");

            migrationBuilder.RenameIndex(
                name: "IX_users_AuthUserId",
                table: "users",
                newName: "IX_users_Auth0UserId");
        }
    }
}
