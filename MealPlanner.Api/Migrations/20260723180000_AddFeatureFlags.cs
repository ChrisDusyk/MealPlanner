using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.Key);
                });

            // Seed a single demonstration flag. Its defaultVariant is "on", so
            // toggling Enabled flips the resolved boolean: enabled -> true,
            // disabled -> the caller's code default (false).
            migrationBuilder.InsertData(
                table: "feature_flags",
                columns: new[] { "Key", "DefinitionJson", "Description", "Enabled", "UpdatedAt" },
                values: new object[]
                {
                    "demo-banner",
                    "{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}",
                    "Shows a demonstration banner in the app shell when enabled.",
                    false,
                    new DateTime(2026, 7, 23, 0, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flags");
        }
    }
}
