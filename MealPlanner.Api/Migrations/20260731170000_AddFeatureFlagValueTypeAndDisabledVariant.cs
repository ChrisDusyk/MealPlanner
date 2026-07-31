using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlagValueTypeAndDisabledVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The JSON kind shared by every variant of a flag. Existing rows are
            // boolean flags, which the default value backfills.
            migrationBuilder.AddColumn<string>(
                name: "ValueType",
                table: "feature_flags",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "boolean");

            // Variant served while a flag is switched off. Left null for existing
            // rows so they keep emitting state: DISABLED and resolving to the
            // caller's code default, exactly as before.
            migrationBuilder.AddColumn<string>(
                name: "DisabledVariant",
                table: "feature_flags",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledVariant",
                table: "feature_flags");

            migrationBuilder.DropColumn(
                name: "ValueType",
                table: "feature_flags");
        }
    }
}
