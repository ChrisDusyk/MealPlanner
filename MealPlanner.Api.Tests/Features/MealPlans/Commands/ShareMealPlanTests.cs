using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using System.Runtime.CompilerServices;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class ShareMealPlanTests
{
	private static ConnectionId CreateConnectionId() =>
		new(new ServerId(new ClusterId(), new System.Net.DnsEndPoint("localhost", 27017)));

	private static MongoWriteException CreateDuplicateKeyWriteException()
	{
		var writeError = (WriteError)RuntimeHelpers.GetUninitializedObject(typeof(WriteError));
		typeof(WriteError).GetField("_category", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, ServerErrorCategory.DuplicateKey);
		typeof(WriteError).GetField("_code", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, 11000);
		typeof(WriteError).GetField("_message", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, "E11000 duplicate key error");
		typeof(WriteError).GetField("_details", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, new BsonDocument());

		return new MongoWriteException(CreateConnectionId(), writeError, null, null);
	}

	private static Mock<IMongoCollection<UserDocument>> CreateUsersCollectionWith(params UserDocument[] docs)
	{
		var users = new Mock<IMongoCollection<UserDocument>>();
		var docList = docs.Select(d => new List<UserDocument> { d }).ToList();
		if (docList.Count == 0)
		{
			var emptyCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)Array.Empty<UserDocument>());
			users.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyCursor.Object);
			users.Setup(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>())).Returns(emptyCursor.Object);
			return users;
		}

		var asyncSetup = users.SetupSequence(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()));
		var syncSetup = users.SetupSequence(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()));
		foreach (var list in docList)
		{
			var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)list);
			asyncSetup = asyncSetup.ReturnsAsync(cursor.Object);
			syncSetup = syncSetup.Returns(cursor.Object);
		}
		return users;
	}

	private static Mock<IMongoCollection<MealPlanShareDocument>> CreateSharesCollectionWithExisting(MealPlanShareDocument? existing)
	{
		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		var indexManager = new Mock<IMongoIndexManager<MealPlanShareDocument>>();
		indexManager.Setup(i => i.CreateOneAsync(It.IsAny<CreateIndexModel<MealPlanShareDocument>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync("ux_shares_owner_recipient_week");
		shares.SetupGet(s => s.Indexes).Returns(indexManager.Object);

		var list = existing is null ? Array.Empty<MealPlanShareDocument>() : new[] { existing };
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)list);
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);
		shares.Setup(c => c.InsertOneAsync(It.IsAny<MealPlanShareDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
		return shares;
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenEmailMissing()
	{
		var handler = new ShareMealPlanCommandHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", " ", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenWeekStartMissing()
	{
		var handler = new ShareMealPlanCommandHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", " ", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenOwnerMissing()
	{
		var users = CreateUsersCollectionWith();
		var shares = CreateSharesCollectionWithExisting(null);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "a@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenSelfShare()
	{
		var owner = new UserDocument { Auth0UserId = "owner1", Name = "Owner", Email = "owner@example.com" };
		var users = CreateUsersCollectionWith(owner, owner);
		var shares = CreateSharesCollectionWithExisting(null);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "owner@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRecipientMissing()
	{
		var owner = new UserDocument { Auth0UserId = "owner1", Name = "Owner", Email = "owner@example.com" };
		var emptyUserCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)Array.Empty<UserDocument>());
		var ownerCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)new List<UserDocument> { owner });

		var users = new Mock<IMongoCollection<UserDocument>>();
		users.SetupSequence(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(ownerCursor.Object)
			.ReturnsAsync(emptyUserCursor.Object);
		users.SetupSequence(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(ownerCursor.Object)
			.Returns(emptyUserCursor.Object);

		var shares = CreateSharesCollectionWithExisting(null);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenShareAlreadyExists()
	{
		var owner = new UserDocument { Auth0UserId = "owner1", Name = "Owner", Email = "owner@example.com" };
		var recipient = new UserDocument { Auth0UserId = "recipient1", Name = "Recipient", Email = "recipient@example.com" };
		var existing = new MealPlanShareDocument { Id = "s1", OwnerUserId = "owner1", SharedWithUserId = "recipient1", WeekStart = "2026-02-23", Permission = nameof(SharePermission.ReadOnly), SharedAt = DateTime.UtcNow };

		var users = CreateUsersCollectionWith(owner, recipient);
		var shares = CreateSharesCollectionWithExisting(existing);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesShare_WhenValid()
	{
		var owner = new UserDocument { Auth0UserId = "owner1", Name = "Owner", Email = "owner@example.com" };
		var recipient = new UserDocument { Auth0UserId = "recipient1", Name = "Recipient", Email = "recipient@example.com" };
		var users = CreateUsersCollectionWith(owner, recipient);
		var shares = CreateSharesCollectionWithExisting(null);
		MealPlanShareDocument? inserted = null;
		shares.Setup(c => c.InsertOneAsync(It.IsAny<MealPlanShareDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.Callback<MealPlanShareDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
			.Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadWrite), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(inserted);
		Assert.Equal("owner1", inserted.OwnerUserId);
		Assert.Equal("recipient1", inserted.SharedWithUserId);
		Assert.Equal(nameof(SharePermission.ReadWrite), inserted.Permission);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ContinuesWhenShareIndexHasConflict()
	{
		var owner = new UserDocument { Auth0UserId = "owner1", Name = "Owner", Email = "owner@example.com" };
		var recipient = new UserDocument { Auth0UserId = "recipient1", Name = "Recipient", Email = "recipient@example.com" };

		var users = CreateUsersCollectionWith(owner, recipient);

		var conflict = new MongoCommandException(
			CreateConnectionId(),
			"Index conflict",
			new BsonDocument { { "codeName", "IndexOptionsConflict" } });

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		var indexManager = new Mock<IMongoIndexManager<MealPlanShareDocument>>();
		indexManager.Setup(i => i.CreateOneAsync(It.IsAny<CreateIndexModel<MealPlanShareDocument>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(conflict);
		shares.SetupGet(s => s.Indexes).Returns(indexManager.Object);

		var emptyCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)Array.Empty<MealPlanShareDocument>());
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).Returns(emptyCursor.Object);
		shares.Setup(c => c.InsertOneAsync(It.IsAny<MealPlanShareDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInsertHitsDuplicateKey()
	{
		var owner = new UserDocument { Auth0UserId = "owner1", Name = "Owner", Email = "owner@example.com" };
		var recipient = new UserDocument { Auth0UserId = "recipient1", Name = "Recipient", Email = "recipient@example.com" };
		var users = CreateUsersCollectionWith(owner, recipient);
		var shares = CreateSharesCollectionWithExisting(null);
		shares.Setup(c => c.InsertOneAsync(It.IsAny<MealPlanShareDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(CreateDuplicateKeyWriteException());

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ShareMealPlanCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
