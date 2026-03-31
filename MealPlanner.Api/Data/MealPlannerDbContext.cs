using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Data;

public class MealPlannerDbContext(DbContextOptions<MealPlannerDbContext> options) : DbContext(options)
{
	public DbSet<UserEntity> Users => Set<UserEntity>();
	public DbSet<FriendshipEntity> Friendships => Set<FriendshipEntity>();
	public DbSet<FriendRequestEntity> FriendRequests => Set<FriendRequestEntity>();
	public DbSet<FriendAutoSharePreferenceEntity> FriendAutoSharePreferences => Set<FriendAutoSharePreferenceEntity>();
	public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
	public DbSet<MealPlanEntity> MealPlans => Set<MealPlanEntity>();
	public DbSet<MealPlanShareEntity> MealPlanShares => Set<MealPlanShareEntity>();
	public DbSet<GroceryListEntity> GroceryLists => Set<GroceryListEntity>();
	public DbSet<GroceryListShareEntity> GroceryListShares => Set<GroceryListShareEntity>();
	public DbSet<GoogleIntegrationConnectionEntity> GoogleIntegrationConnections => Set<GoogleIntegrationConnectionEntity>();
	public DbSet<GroceryListExportLinkEntity> GroceryListExportLinks => Set<GroceryListExportLinkEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// ── Users ──
		modelBuilder.Entity<UserEntity>(entity =>
		{
			entity.ToTable("users");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.Auth0UserId).IsUnique();
		});

		// ── Friendships ──
		modelBuilder.Entity<FriendshipEntity>(entity =>
		{
			entity.ToTable("friendships");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserAId, e.UserBId }).IsUnique();
		});

		// ── Friend Requests ──
		modelBuilder.Entity<FriendRequestEntity>(entity =>
		{
			entity.ToTable("friend_requests");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.RequesterUserId, e.RecipientUserId }).IsUnique();
		});

		// ── Friend Auto-Share Preferences ──
		modelBuilder.Entity<FriendAutoSharePreferenceEntity>(entity =>
		{
			entity.ToTable("friend_auto_share_preferences");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserId, e.FriendUserId }).IsUnique();
		});

		// ── Recipes ──
		modelBuilder.Entity<RecipeEntity>(entity =>
		{
			entity.ToTable("recipes");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.UserId);
			entity.Property(e => e.Ingredients).HasColumnType("jsonb");
		});

		// ── Meal Plans ──
		modelBuilder.Entity<MealPlanEntity>(entity =>
		{
			entity.ToTable("meal_plans");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserId, e.WeekStart }).IsUnique();
			entity.Property(e => e.Days).HasColumnType("jsonb");
		});

		// ── Meal Plan Shares ──
		modelBuilder.Entity<MealPlanShareEntity>(entity =>
		{
			entity.ToTable("meal_plan_shares");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.OwnerUserId, e.SharedWithUserId, e.WeekStart }).IsUnique();
		});

		// ── Grocery Lists ──
		modelBuilder.Entity<GroceryListEntity>(entity =>
		{
			entity.ToTable("grocery_lists");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserId, e.WeekStart }).IsUnique();
			entity.Property(e => e.Items).HasColumnType("jsonb");
			entity.Property(e => e.PantryStapleItems).HasColumnType("jsonb");
		});

		// ── Grocery List Shares ──
		modelBuilder.Entity<GroceryListShareEntity>(entity =>
		{
			entity.ToTable("grocery_list_shares");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.OwnerUserId, e.SharedWithUserId, e.WeekStart }).IsUnique();
		});

		// ── Google Integration Connections ──
		modelBuilder.Entity<GoogleIntegrationConnectionEntity>(entity =>
		{
			entity.ToTable("google_integration_connections");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();
			entity.HasIndex(e => new { e.GoogleSubject, e.Provider }).IsUnique();
			entity.Property(e => e.Scopes).HasColumnType("jsonb");
		});

		// ── Grocery List Export Links ──
		modelBuilder.Entity<GroceryListExportLinkEntity>(entity =>
		{
			entity.ToTable("grocery_list_export_links");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserId, e.WeekStart, e.Provider }).IsUnique();
			entity.HasIndex(e => new { e.UserId, e.GroceryListId, e.Provider }).IsUnique();
		});
	}
}
