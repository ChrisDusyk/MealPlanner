using System;
using System.Collections.Generic;
using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogueRecipeId",
                table: "recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalogue_recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Servings = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Ingredients = table.Column<List<IngredientData>>(type: "jsonb", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalogue_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalogue_recipe_tags",
                columns: table => new
                {
                    CatalogueRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_recipe_tags", x => new { x.CatalogueRecipeId, x.TagId });
                    table.ForeignKey(
                        name: "FK_catalogue_recipe_tags_catalogue_recipes_CatalogueRecipeId",
                        column: x => x.CatalogueRecipeId,
                        principalTable: "catalogue_recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalogue_recipe_tags_catalogue_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "catalogue_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipes_UserId_CatalogueRecipeId",
                table: "recipes",
                columns: new[] { "UserId", "CatalogueRecipeId" },
                unique: true,
                filter: "\"CatalogueRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_recipe_tags_TagId",
                table: "catalogue_recipe_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_recipes_IsPublished",
                table: "catalogue_recipes",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_recipes_PublishedAt",
                table: "catalogue_recipes",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_tags_Slug",
                table: "catalogue_tags",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogue_recipe_tags");

            migrationBuilder.DropTable(
                name: "catalogue_recipes");

            migrationBuilder.DropTable(
                name: "catalogue_tags");

            migrationBuilder.DropIndex(
                name: "IX_recipes_UserId_CatalogueRecipeId",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "CatalogueRecipeId",
                table: "recipes");
        }
    }
}
