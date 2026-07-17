using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop the per-week sharing + friends system ──
            migrationBuilder.DropTable(
                name: "friend_auto_share_preferences");

            migrationBuilder.DropTable(
                name: "friend_requests");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "grocery_list_shares");

            migrationBuilder.DropTable(
                name: "meal_plan_shares");

            // ── 2. Create the family group tables ──
            migrationBuilder.CreateTable(
                name: "family_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "family_group_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_family_group_members_family_groups_FamilyGroupId",
                        column: x => x.FamilyGroupId,
                        principalTable: "family_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "family_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<string>(type: "text", nullable: false),
                    InviteeUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_family_invitations_family_groups_FamilyGroupId",
                        column: x => x.FamilyGroupId,
                        principalTable: "family_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── 3. Provision a single-member personal family for every existing user ──
            migrationBuilder.Sql("""
                INSERT INTO family_groups ("Id", "Name", "OwnerUserId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(),
                       CASE
                           WHEN COALESCE(NULLIF(TRIM(u."DisplayName"), ''), NULLIF(TRIM(u."Name"), '')) IS NULL
                               THEN 'My Family'
                           ELSE COALESCE(NULLIF(TRIM(u."DisplayName"), ''), NULLIF(TRIM(u."Name"), '')) || '''s Family'
                       END,
                       u."AuthUserId",
                       NOW(),
                       NOW()
                FROM users u;
                """);

            migrationBuilder.Sql("""
                INSERT INTO family_group_members ("Id", "FamilyGroupId", "UserId", "JoinedAt")
                SELECT gen_random_uuid(), f."Id", f."OwnerUserId", NOW()
                FROM family_groups f;
                """);

            // ── 4. Re-key content tables from user ids to family group ids ──
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "recipes",
                newName: "ContributedByUserId");

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyGroupId",
                table: "recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyGroupId",
                table: "meal_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyGroupId",
                table: "grocery_lists",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE recipes r
                SET "FamilyGroupId" = m."FamilyGroupId"
                FROM family_group_members m
                WHERE m."UserId" = r."ContributedByUserId";
                """);

            migrationBuilder.Sql("""
                UPDATE meal_plans p
                SET "FamilyGroupId" = m."FamilyGroupId"
                FROM family_group_members m
                WHERE m."UserId" = p."UserId";
                """);

            migrationBuilder.Sql("""
                UPDATE grocery_lists g
                SET "FamilyGroupId" = m."FamilyGroupId"
                FROM family_group_members m
                WHERE m."UserId" = g."UserId";
                """);

            // Rows whose owner has no user record cannot be re-keyed; drop them.
            migrationBuilder.Sql("""DELETE FROM recipes WHERE "FamilyGroupId" IS NULL;""");
            migrationBuilder.Sql("""DELETE FROM meal_plans WHERE "FamilyGroupId" IS NULL;""");
            migrationBuilder.Sql("""DELETE FROM grocery_lists WHERE "FamilyGroupId" IS NULL;""");

            migrationBuilder.AlterColumn<Guid>(
                name: "FamilyGroupId",
                table: "recipes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FamilyGroupId",
                table: "meal_plans",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FamilyGroupId",
                table: "grocery_lists",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // ── 5. Swap the old user-keyed indexes for family-keyed ones ──
            migrationBuilder.DropIndex(
                name: "IX_recipes_UserId",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "IX_recipes_UserId_CatalogueRecipeId",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "IX_meal_plans_UserId_WeekStart",
                table: "meal_plans");

            migrationBuilder.DropIndex(
                name: "IX_grocery_lists_UserId_WeekStart",
                table: "grocery_lists");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "grocery_lists");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_FamilyGroupId",
                table: "recipes",
                column: "FamilyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_FamilyGroupId_CatalogueRecipeId",
                table: "recipes",
                columns: new[] { "FamilyGroupId", "CatalogueRecipeId" },
                unique: true,
                filter: "\"CatalogueRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_FamilyGroupId_WeekStart",
                table: "meal_plans",
                columns: new[] { "FamilyGroupId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grocery_lists_FamilyGroupId_WeekStart",
                table: "grocery_lists",
                columns: new[] { "FamilyGroupId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_family_group_members_FamilyGroupId",
                table: "family_group_members",
                column: "FamilyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_family_group_members_UserId",
                table: "family_group_members",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_family_groups_OwnerUserId",
                table: "family_groups",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_family_invitations_FamilyGroupId_InviteeUserId",
                table: "family_invitations",
                columns: new[] { "FamilyGroupId", "InviteeUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_family_invitations_InviteeUserId",
                table: "family_invitations",
                column: "InviteeUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_grocery_lists_family_groups_FamilyGroupId",
                table: "grocery_lists",
                column: "FamilyGroupId",
                principalTable: "family_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_meal_plans_family_groups_FamilyGroupId",
                table: "meal_plans",
                column: "FamilyGroupId",
                principalTable: "family_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_grocery_lists_family_groups_FamilyGroupId",
                table: "grocery_lists");

            migrationBuilder.DropForeignKey(
                name: "FK_meal_plans_family_groups_FamilyGroupId",
                table: "meal_plans");

            migrationBuilder.DropTable(
                name: "family_group_members");

            migrationBuilder.DropTable(
                name: "family_invitations");

            migrationBuilder.DropTable(
                name: "family_groups");

            migrationBuilder.DropIndex(
                name: "IX_recipes_FamilyGroupId",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "IX_recipes_FamilyGroupId_CatalogueRecipeId",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "IX_meal_plans_FamilyGroupId_WeekStart",
                table: "meal_plans");

            migrationBuilder.DropIndex(
                name: "IX_grocery_lists_FamilyGroupId_WeekStart",
                table: "grocery_lists");

            migrationBuilder.DropColumn(
                name: "FamilyGroupId",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "FamilyGroupId",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "FamilyGroupId",
                table: "grocery_lists");

            migrationBuilder.RenameColumn(
                name: "ContributedByUserId",
                table: "recipes",
                newName: "UserId");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "meal_plans",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "grocery_lists",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "friend_auto_share_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutoShareGroceryLists = table.Column<bool>(type: "boolean", nullable: false),
                    AutoShareMealPlans = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FriendUserId = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friend_auto_share_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "friend_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecipientUserId = table.Column<string>(type: "text", nullable: false),
                    RequesterUserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friend_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserAId = table.Column<string>(type: "text", nullable: false),
                    UserBId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friendships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grocery_list_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DismissedByRecipient = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    Permission = table.Column<string>(type: "text", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "text", nullable: false),
                    WeekStart = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grocery_list_shares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DismissedByRecipient = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    Permission = table.Column<string>(type: "text", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "text", nullable: false),
                    WeekStart = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_shares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipes_UserId",
                table: "recipes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_UserId_CatalogueRecipeId",
                table: "recipes",
                columns: new[] { "UserId", "CatalogueRecipeId" },
                unique: true,
                filter: "\"CatalogueRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_UserId_WeekStart",
                table: "meal_plans",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grocery_lists_UserId_WeekStart",
                table: "grocery_lists",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_friend_auto_share_preferences_UserId_FriendUserId",
                table: "friend_auto_share_preferences",
                columns: new[] { "UserId", "FriendUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_friend_requests_RequesterUserId_RecipientUserId",
                table: "friend_requests",
                columns: new[] { "RequesterUserId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_friendships_UserAId_UserBId",
                table: "friendships",
                columns: new[] { "UserAId", "UserBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grocery_list_shares_OwnerUserId_SharedWithUserId_WeekStart",
                table: "grocery_list_shares",
                columns: new[] { "OwnerUserId", "SharedWithUserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_shares_OwnerUserId_SharedWithUserId_WeekStart",
                table: "meal_plan_shares",
                columns: new[] { "OwnerUserId", "SharedWithUserId", "WeekStart" },
                unique: true);
        }
    }
}
