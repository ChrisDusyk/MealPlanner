using System.Security.Cryptography;
using System.Text;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Users.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace MealPlanner.Api.Data;

public sealed class MongoToPostgresMigrationHostedService(
	IMongoClient mongoClient,
	IConfiguration configuration,
	IServiceScopeFactory scopeFactory,
	ILogger<MongoToPostgresMigrationHostedService> logger)
	: IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!configuration.GetValue<bool>("MongoToPostgresMigration:Enabled"))
			return;

		logger.LogInformation("Starting MongoDB to PostgreSQL data migration job");

		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
		var mongoDb = mongoClient.GetDatabase("mealplannerDb");

		await MigrateUsersAsync(db, mongoDb, cancellationToken);
		await MigrateFriendshipsAsync(db, mongoDb, cancellationToken);
		await MigrateFriendRequestsAsync(db, mongoDb, cancellationToken);
		await MigrateAutoSharePreferencesAsync(db, mongoDb, cancellationToken);
		await MigrateRecipesAsync(db, mongoDb, cancellationToken);
		await MigrateMealPlansAsync(db, mongoDb, cancellationToken);
		await MigrateMealPlanSharesAsync(db, mongoDb, cancellationToken);
		await MigrateGroceryListsAsync(db, mongoDb, cancellationToken);
		await MigrateGroceryListSharesAsync(db, mongoDb, cancellationToken);
		await MigrateGoogleConnectionsAsync(db, mongoDb, cancellationToken);
		await MigrateExportLinksAsync(db, mongoDb, cancellationToken);

		logger.LogInformation("MongoDB to PostgreSQL data migration job completed");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private static async Task MigrateUsersAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<UserDocument>("users").Find(_ => true).ToListAsync(ct);
		var entities = await db.Users.ToListAsync(ct);
		var byAuth0 = entities.ToDictionary(x => x.Auth0UserId, StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			if (!byAuth0.TryGetValue(doc.Auth0UserId, out var entity))
			{
				entity = new UserEntity { Id = ToGuid(doc.Id, $"user:{doc.Auth0UserId}"), Auth0UserId = doc.Auth0UserId };
				db.Users.Add(entity);
				byAuth0[doc.Auth0UserId] = entity;
			}

			entity.Name = doc.Name;
			entity.Email = doc.Email;
			entity.CreatedAt = doc.CreatedAt;
			entity.UpdatedAt = doc.UpdatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateFriendshipsAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<FriendshipDocument>("friendships").Find(_ => true).ToListAsync(ct);
		var entities = await db.Friendships.ToListAsync(ct);
		var byPair = entities.ToDictionary(x => $"{x.UserAId}|{x.UserBId}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.UserAId}|{doc.UserBId}";
			if (!byPair.TryGetValue(key, out var entity))
			{
				entity = new FriendshipEntity
				{
					Id = ToGuid(doc.Id, $"friendship:{key}"),
					UserAId = doc.UserAId,
					UserBId = doc.UserBId
				};
				db.Friendships.Add(entity);
				byPair[key] = entity;
			}

			entity.CreatedAt = doc.CreatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateFriendRequestsAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<FriendRequestDocument>("friend_requests").Find(_ => true).ToListAsync(ct);
		var entities = await db.FriendRequests.ToListAsync(ct);
		var byPair = entities.ToDictionary(x => $"{x.RequesterUserId}|{x.RecipientUserId}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.RequesterUserId}|{doc.RecipientUserId}";
			if (!byPair.TryGetValue(key, out var entity))
			{
				entity = new FriendRequestEntity
				{
					Id = ToGuid(doc.Id, $"friend-request:{key}"),
					RequesterUserId = doc.RequesterUserId,
					RecipientUserId = doc.RecipientUserId
				};
				db.FriendRequests.Add(entity);
				byPair[key] = entity;
			}

			entity.CreatedAt = doc.CreatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateAutoSharePreferencesAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences").Find(_ => true).ToListAsync(ct);
		var entities = await db.FriendAutoSharePreferences.ToListAsync(ct);
		var byPair = entities.ToDictionary(x => $"{x.UserId}|{x.FriendUserId}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.UserId}|{doc.FriendUserId}";
			if (!byPair.TryGetValue(key, out var entity))
			{
				entity = new FriendAutoSharePreferenceEntity
				{
					Id = ToGuid(doc.Id, $"friend-pref:{key}"),
					UserId = doc.UserId,
					FriendUserId = doc.FriendUserId
				};
				db.FriendAutoSharePreferences.Add(entity);
				byPair[key] = entity;
			}

			entity.AutoShareMealPlans = doc.AutoShareMealPlans;
			entity.AutoShareGroceryLists = doc.AutoShareGroceryLists;
			entity.CreatedAt = doc.CreatedAt;
			entity.UpdatedAt = doc.UpdatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateRecipesAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<RecipeDocument>("recipes").Find(_ => true).ToListAsync(ct);
		var entities = await db.Recipes.ToListAsync(ct);
		var byId = entities.ToDictionary(x => x.Id);

		foreach (var doc in documents)
		{
			var recipeId = ToGuid(doc.Id, $"recipe:{doc.UserId}:{doc.Name}:{doc.CreatedAt:o}");
			if (!byId.TryGetValue(recipeId, out var entity))
			{
				entity = new RecipeEntity { Id = recipeId };
				db.Recipes.Add(entity);
				byId[recipeId] = entity;
			}

			entity.UserId = doc.UserId;
			entity.Name = doc.Name;
			entity.Description = doc.Description;
			entity.Servings = doc.Servings;
			entity.SourceUrl = doc.SourceUrl;
			entity.Ingredients = doc.Ingredients.Select(i => new IngredientData
			{
				Name = i.Name,
				Quantity = i.Quantity,
				Unit = i.Unit,
				IsPantryStaple = i.IsPantryStaple
			}).ToList();
			entity.CreatedAt = doc.CreatedAt;
			entity.UpdatedAt = doc.UpdatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateMealPlansAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<MealPlanDocument>("mealplans").Find(_ => true).ToListAsync(ct);
		var entities = await db.MealPlans.ToListAsync(ct);
		var byUserWeek = entities.ToDictionary(x => $"{x.UserId}|{x.WeekStart}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.UserId}|{doc.WeekStart}";
			if (!byUserWeek.TryGetValue(key, out var entity))
			{
				entity = new MealPlanEntity
				{
					Id = ToGuid(doc.Id, $"mealplan:{key}"),
					UserId = doc.UserId,
					WeekStart = doc.WeekStart
				};
				db.MealPlans.Add(entity);
				byUserWeek[key] = entity;
			}

			entity.Days = doc.Days.Select(d => new DayPlanData
			{
				Day = d.Day,
				Slots = d.Slots.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value.Select(item => new MealSlotItemData
					{
						RecipeId = NormalizeRecipeId(item.RecipeId),
						Name = item.Name,
						Servings = item.Servings
					}).ToList())
			}).ToList();
			entity.CreatedAt = doc.CreatedAt;
			entity.UpdatedAt = doc.UpdatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateMealPlanSharesAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<MealPlanShareDocument>("shares").Find(_ => true).ToListAsync(ct);
		var entities = await db.MealPlanShares.ToListAsync(ct);
		var byKey = entities.ToDictionary(x => $"{x.OwnerUserId}|{x.SharedWithUserId}|{x.WeekStart}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.OwnerUserId}|{doc.SharedWithUserId}|{doc.WeekStart}";
			if (!byKey.TryGetValue(key, out var entity))
			{
				entity = new MealPlanShareEntity
				{
					Id = ToGuid(doc.Id, $"meal-share:{key}"),
					OwnerUserId = doc.OwnerUserId,
					SharedWithUserId = doc.SharedWithUserId,
					WeekStart = doc.WeekStart
				};
				db.MealPlanShares.Add(entity);
				byKey[key] = entity;
			}

			entity.Permission = doc.Permission;
			entity.SharedAt = doc.SharedAt;
			entity.DismissedByRecipient = doc.DismissedByRecipient;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateGroceryListsAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<GroceryListDocument>("grocerylists").Find(_ => true).ToListAsync(ct);
		var entities = await db.GroceryLists.ToListAsync(ct);
		var byUserWeek = entities.ToDictionary(x => $"{x.UserId}|{x.WeekStart}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.UserId}|{doc.WeekStart}";
			if (!byUserWeek.TryGetValue(key, out var entity))
			{
				entity = new GroceryListEntity
				{
					Id = ToGuid(doc.Id, $"grocery:{key}"),
					UserId = doc.UserId,
					WeekStart = doc.WeekStart
				};
				db.GroceryLists.Add(entity);
				byUserWeek[key] = entity;
			}

			entity.Items = doc.Items.Select(i => new GroceryListItemData
			{
				Name = i.Name,
				Quantity = i.Quantity,
				Unit = i.Unit,
				IsChecked = i.IsChecked,
				SourceRecipeNames = i.SourceRecipeNames
			}).ToList();
			entity.PantryStapleItems = doc.PantryStapleItems.Select(i => new GroceryListItemData
			{
				Name = i.Name,
				Quantity = i.Quantity,
				Unit = i.Unit,
				IsChecked = i.IsChecked,
				SourceRecipeNames = i.SourceRecipeNames
			}).ToList();
			entity.CreatedAt = doc.CreatedAt;
			entity.UpdatedAt = doc.UpdatedAt;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateGroceryListSharesAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<GroceryListShareDocument>("grocerylist_shares").Find(_ => true).ToListAsync(ct);
		var entities = await db.GroceryListShares.ToListAsync(ct);
		var byKey = entities.ToDictionary(x => $"{x.OwnerUserId}|{x.SharedWithUserId}|{x.WeekStart}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.OwnerUserId}|{doc.SharedWithUserId}|{doc.WeekStart}";
			if (!byKey.TryGetValue(key, out var entity))
			{
				entity = new GroceryListShareEntity
				{
					Id = ToGuid(doc.Id, $"grocery-share:{key}"),
					OwnerUserId = doc.OwnerUserId,
					SharedWithUserId = doc.SharedWithUserId,
					WeekStart = doc.WeekStart
				};
				db.GroceryListShares.Add(entity);
				byKey[key] = entity;
			}

			entity.Permission = doc.Permission;
			entity.SharedAt = doc.SharedAt;
			entity.DismissedByRecipient = doc.DismissedByRecipient;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateGoogleConnectionsAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<GoogleIntegrationConnectionDocument>("google_integration_connections").Find(_ => true).ToListAsync(ct);
		var entities = await db.GoogleIntegrationConnections.ToListAsync(ct);
		var byUserProvider = entities.ToDictionary(x => $"{x.UserId}|{x.Provider}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.UserId}|{doc.Provider}";
			if (!byUserProvider.TryGetValue(key, out var entity))
			{
				entity = new GoogleIntegrationConnectionEntity
				{
					Id = ToGuid(doc.Id, $"google-conn:{key}"),
					UserId = doc.UserId,
					Provider = doc.Provider
				};
				db.GoogleIntegrationConnections.Add(entity);
				byUserProvider[key] = entity;
			}

			entity.GoogleSubject = doc.GoogleSubject;
			entity.GoogleEmail = doc.GoogleEmail;
			entity.EncryptedAccessToken = doc.EncryptedAccessToken;
			entity.EncryptedRefreshToken = doc.EncryptedRefreshToken;
			entity.AccessTokenExpiresAtUtc = doc.AccessTokenExpiresAtUtc;
			entity.Scopes = doc.Scopes;
			entity.Capability = doc.Capability;
			entity.CreatedAt = doc.CreatedAt;
			entity.UpdatedAt = doc.UpdatedAt;
			entity.DisconnectedAtUtc = doc.DisconnectedAtUtc;
		}

		await db.SaveChangesAsync(ct);
	}

	private static async Task MigrateExportLinksAsync(MealPlannerDbContext db, IMongoDatabase mongoDb, CancellationToken ct)
	{
		var documents = await mongoDb.GetCollection<GroceryListExportLinkDocument>("grocerylist_export_links").Find(_ => true).ToListAsync(ct);
		var entities = await db.GroceryListExportLinks.ToListAsync(ct);
		var byKey = entities.ToDictionary(x => $"{x.UserId}|{x.WeekStart}|{x.Provider}", StringComparer.Ordinal);

		foreach (var doc in documents)
		{
			var key = $"{doc.UserId}|{doc.WeekStart}|{doc.Provider}";
			if (!byKey.TryGetValue(key, out var entity))
			{
				entity = new GroceryListExportLinkEntity
				{
					Id = ToGuid(doc.Id, $"export-link:{key}"),
					UserId = doc.UserId,
					WeekStart = doc.WeekStart,
					Provider = doc.Provider
				};
				db.GroceryListExportLinks.Add(entity);
				byKey[key] = entity;
			}

			entity.GroceryListId = NormalizeGroceryListId(doc.GroceryListId);
			entity.ExternalItemId = doc.ExternalItemId;
			entity.LastExportedAtUtc = doc.LastExportedAtUtc;
			entity.LastSyncHash = doc.LastSyncHash;
		}

		await db.SaveChangesAsync(ct);
	}

	private static string? NormalizeRecipeId(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return Guid.TryParse(value, out var parsed)
			? parsed.ToString()
			: ToGuid(value, $"recipe-ref:{value}").ToString();
	}

	private static string NormalizeGroceryListId(string value)
	{
		if (Guid.TryParse(value, out var parsed))
			return parsed.ToString();

		return ToGuid(value, $"grocery-list-ref:{value}").ToString();
	}

	private static Guid ToGuid(string? source, string fallbackSeed)
	{
		if (!string.IsNullOrWhiteSpace(source) && Guid.TryParse(source, out var guid))
			return guid;

		var seed = string.IsNullOrWhiteSpace(source) ? fallbackSeed : source;
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
		Span<byte> bytes = stackalloc byte[16];
		hash.AsSpan(0, 16).CopyTo(bytes);
		return new Guid(bytes);
	}
}
