using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

public record DisconnectGoogleKeepConnectionCommand(string UserId) : ICommand<Unit>;

public class DisconnectGoogleKeepConnectionCommandHandler(
	IMongoClient mongoClient,
	IGoogleOAuthService googleOAuthService,
	IIntegrationTokenProtector tokenProtector)
	: ICommandHandler<DisconnectGoogleKeepConnectionCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DisconnectGoogleKeepConnectionCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.UserId))
			return Result<Unit>.Failure(new Error(ErrorCodes.ValidationFailed, "UserId is required."));

		try
		{
			var collection = mongoClient.GetDatabase("mealplannerDb")
				.GetCollection<GoogleIntegrationConnectionDocument>(IntegrationCollections.Connections);

			var current = await collection.Find(c =>
				c.UserId == command.UserId
				&& c.Provider == IntegrationProvider.GoogleKeep.ToString()
				&& c.DisconnectedAtUtc == null).FirstOrDefaultAsync(cancellationToken);

			if (current is null)
				return Result<Unit>.Success(Unit.Value);

			if (!string.IsNullOrWhiteSpace(current.EncryptedRefreshToken))
			{
				var refreshToken = tokenProtector.Unprotect(current.EncryptedRefreshToken);
				if (refreshToken.IsSuccess)
					_ = await googleOAuthService.RevokeTokenAsync(refreshToken.Value!, cancellationToken);
			}
			else if (!string.IsNullOrWhiteSpace(current.EncryptedAccessToken))
			{
				var accessToken = tokenProtector.Unprotect(current.EncryptedAccessToken);
				if (accessToken.IsSuccess)
					_ = await googleOAuthService.RevokeTokenAsync(accessToken.Value!, cancellationToken);
			}

			var update = Builders<GoogleIntegrationConnectionDocument>.Update
				.Set(x => x.EncryptedAccessToken, string.Empty)
				.Set(x => x.EncryptedRefreshToken, null)
				.Set(x => x.AccessTokenExpiresAtUtc, null)
				.Set(x => x.DisconnectedAtUtc, DateTime.UtcNow)
				.Set(x => x.UpdatedAt, DateTime.UtcNow)
				.Set(x => x.Capability, IntegrationCapability.Unknown.ToString());

			await collection.UpdateOneAsync(
				x => x.Id == current.Id,
				update,
				cancellationToken: cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(new Error(
				ErrorCodes.DatabaseError,
				"Failed to disconnect Google Keep integration.",
				ex));
		}
	}
}
