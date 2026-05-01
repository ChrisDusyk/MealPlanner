using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace MealPlanner.Api.Data;

public class MealPlannerDbContext(DbContextOptions<MealPlannerDbContext> options) : DbContext(options)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
	public DbSet<CatalogueRecipeEntity> CatalogueRecipes => Set<CatalogueRecipeEntity>();
	public DbSet<CatalogueTagEntity> CatalogueTags => Set<CatalogueTagEntity>();
	public DbSet<CatalogueRecipeTagEntity> CatalogueRecipeTags => Set<CatalogueRecipeTagEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		var isNpgsql = Database.IsNpgsql();

		// ── Users ──
		modelBuilder.Entity<UserEntity>(entity =>
		{
			entity.ToTable("users");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.AuthUserId).IsUnique();
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
			// Enforces one-copy-per-user when a recipe is added from the catalogue.
			// In Postgres, this is enforced by the filtered unique index below when
			// CatalogueRecipeId is not null. EF Core's InMemory provider does not
			// enforce unique indexes/constraints, so tests rely on the explicit
			// duplicate-prevention check in application code instead.
			var catalogueIndex = entity.HasIndex(e => new { e.UserId, e.CatalogueRecipeId })
				.IsUnique();
			if (isNpgsql)
			{
				catalogueIndex.HasFilter("\"CatalogueRecipeId\" IS NOT NULL");
			}
			if (isNpgsql)
			{
				ConfigureNpgsqlJsonProperty(entity.Property(e => e.Ingredients));
			}
			else
			{
				ConfigureJsonProperty(entity.Property(e => e.Ingredients));
			}
		});

		// ── Catalogue Recipes ──
		modelBuilder.Entity<CatalogueRecipeEntity>(entity =>
		{
			entity.ToTable("catalogue_recipes");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.IsPublished);
			entity.HasIndex(e => e.PublishedAt);
			if (isNpgsql)
			{
				ConfigureNpgsqlJsonProperty(entity.Property(e => e.Ingredients));
			}
			else
			{
				ConfigureJsonProperty(entity.Property(e => e.Ingredients));
			}
		});

		// ── Catalogue Tags ──
		modelBuilder.Entity<CatalogueTagEntity>(entity =>
		{
			entity.ToTable("catalogue_tags");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.Slug).IsUnique();
		});

		// ── Catalogue Recipe ↔ Tag join ──
		modelBuilder.Entity<CatalogueRecipeTagEntity>(entity =>
		{
			entity.ToTable("catalogue_recipe_tags");
			entity.HasKey(e => new { e.CatalogueRecipeId, e.TagId });
			entity.HasOne(e => e.CatalogueRecipe)
				.WithMany(r => r.Tags)
				.HasForeignKey(e => e.CatalogueRecipeId)
				.OnDelete(DeleteBehavior.Cascade);
			entity.HasOne(e => e.Tag)
				.WithMany(t => t.RecipeTags)
				.HasForeignKey(e => e.TagId)
				.OnDelete(DeleteBehavior.Restrict);
			entity.HasIndex(e => e.TagId);
		});

		// ── Meal Plans ──
		modelBuilder.Entity<MealPlanEntity>(entity =>
		{
			entity.ToTable("meal_plans");
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => new { e.UserId, e.WeekStart }).IsUnique();
			if (isNpgsql)
			{
				ConfigureNpgsqlJsonProperty(entity.Property(e => e.Days));
			}
			else
			{
				ConfigureJsonProperty(entity.Property(e => e.Days));
			}
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
			if (isNpgsql)
			{
				ConfigureNpgsqlJsonProperty(entity.Property(e => e.Items));
				ConfigureNpgsqlJsonProperty(entity.Property(e => e.PantryStapleItems));
			}
			else
			{
				ConfigureJsonProperty(entity.Property(e => e.Items));
				ConfigureJsonProperty(entity.Property(e => e.PantryStapleItems));
			}
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
			if (isNpgsql)
			{
				ConfigureNpgsqlJsonProperty(entity.Property(e => e.Scopes));
			}
			else
			{
				ConfigureJsonProperty(entity.Property(e => e.Scopes));
			}
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

	private static void ConfigureJsonProperty<T>(PropertyBuilder<T> property)
		where T : class, new()
	{
		property.HasConversion(
			value => JsonSerializer.Serialize(value, JsonOptions),
			value => string.IsNullOrWhiteSpace(value)
				? new T()
				: JsonSerializer.Deserialize<T>(value, JsonOptions) ?? new T());

		SetJsonValueComparer(property);
	}

	private static void ConfigureNpgsqlJsonProperty<T>(PropertyBuilder<T> property)
		where T : class, new()
	{
		property.HasColumnType("jsonb");
		SetJsonValueComparer(property);
	}

	private static void SetJsonValueComparer<T>(PropertyBuilder<T> property)
		where T : class, new()
	{
		property.Metadata.SetValueComparer(new ValueComparer<T>(
			(left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
			value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(),
			value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions) ?? new T()));
	}
}
