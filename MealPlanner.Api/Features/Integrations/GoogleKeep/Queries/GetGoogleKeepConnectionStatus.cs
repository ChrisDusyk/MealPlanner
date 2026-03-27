using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Queries;

public record GetGoogleKeepConnectionStatusQuery(string UserId) : IQuery<GoogleKeepConnectionStatus>;

public record GoogleKeepConnectionStatus(
	bool IsConnected,
	IntegrationProvider Provider,
	IntegrationCapability Capability,
	Option<string> ConnectedEmail,
	Option<DateTime> ConnectedAtUtc);

public class GetGoogleKeepConnectionStatusQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetGoogleKeepConnectionStatusQuery, GoogleKeepConnectionStatus>
{
	public async Task<Result<GoogleKeepConnectionStatus>> HandleAsync(
		GetGoogleKeepConnectionStatusQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.UserId))
		{
			return Result<GoogleKeepConnectionStatus>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"UserId is required."));
		}

		try
		{
			var collection = mongoClient.GetDatabase("mealplannerDb")
				.GetCollection<GoogleIntegrationConnectionDocument>(IntegrationCollections.Connections);

			var document = await collection
				.Find(c => c.UserId == query.UserId
				           && c.Provider == IntegrationProvider.GoogleKeep.ToString()
				           && c.DisconnectedAtUtc == null)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
			{
				return Result<GoogleKeepConnectionStatus>.Success(new GoogleKeepConnectionStatus(
					IsConnected: false,
					Provider: IntegrationProvider.GoogleKeep,
					Capability: IntegrationCapability.Unknown,
					ConnectedEmail: Option<string>.None(),
					ConnectedAtUtc: Option<DateTime>.None()));
			}

			_ = Enum.TryParse<IntegrationCapability>(document.Capability, true, out var capability);
			return Result<GoogleKeepConnectionStatus>.Success(new GoogleKeepConnectionStatus(
				IsConnected: true,
				Provider: IntegrationProvider.GoogleKeep,
				Capability: capability,
				ConnectedEmail: Option<string>.From(document.GoogleEmail),
				ConnectedAtUtc: Option<DateTime>.From(document.CreatedAt)));
		}
		catch (Exception ex)
		{
			return Result<GoogleKeepConnectionStatus>.Failure(new Error(
				ErrorCodes.DatabaseError,
				"Failed to load Google Keep connection status.",
				ex));
		}
	}
}
