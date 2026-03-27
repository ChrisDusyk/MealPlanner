using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Queries;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

public record CompleteGoogleKeepConnectionCommand(string UserId, string AuthorizationCode, string State)
	: ICommand<GoogleKeepConnectionStatus>;

public class CompleteGoogleKeepConnectionCommandHandler(
	IMongoClient mongoClient,
	IGoogleOAuthService googleOAuthService,
	IGoogleKeepClient googleKeepClient,
	IIntegrationTokenProtector tokenProtector)
	: ICommandHandler<CompleteGoogleKeepConnectionCommand, GoogleKeepConnectionStatus>
{
	public async Task<Result<GoogleKeepConnectionStatus>> HandleAsync(
		CompleteGoogleKeepConnectionCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.UserId))
			return Result<GoogleKeepConnectionStatus>.Failure(new Error(ErrorCodes.ValidationFailed, "UserId is required."));
		if (string.IsNullOrWhiteSpace(command.AuthorizationCode))
			return Result<GoogleKeepConnectionStatus>.Failure(new Error(ErrorCodes.ValidationFailed, "Authorization code is required."));

		var stateValidation = googleOAuthService.ValidateStateAndGetUserId(command.State);
		if (!stateValidation.IsSuccess)
			return Result<GoogleKeepConnectionStatus>.Failure(stateValidation.Error!);

		if (!string.Equals(stateValidation.Value, command.UserId, StringComparison.Ordinal))
			return Result<GoogleKeepConnectionStatus>.Failure(new Error(ErrorCodes.ValidationFailed, "OAuth state does not match current user."));

		var tokenResult = await googleOAuthService.ExchangeCodeAsync(command.AuthorizationCode, cancellationToken);
		if (!tokenResult.IsSuccess)
			return Result<GoogleKeepConnectionStatus>.Failure(tokenResult.Error!);

		var capabilityResult = await googleKeepClient.GetCapabilityAsync(tokenResult.Value!.AccessToken, cancellationToken);
		if (!capabilityResult.IsSuccess && capabilityResult.Error?.Code == ErrorCodes.Unauthorized)
			return Result<GoogleKeepConnectionStatus>.Failure(capabilityResult.Error!);

		var capability = capabilityResult.IsSuccess
			? capabilityResult.Value!
			: IntegrationCapability.Unknown;

		try
		{
			var collection = mongoClient.GetDatabase("mealplannerDb")
				.GetCollection<GoogleIntegrationConnectionDocument>(IntegrationCollections.Connections);

			var now = DateTime.UtcNow;
			var tokenValue = tokenResult.Value;
			var googleSubject = tokenValue.GoogleSubject.GetValueOrDefault(command.UserId);
			var encryptedRefreshToken = tokenValue.RefreshToken.Match(
				onSome: value => (string?)tokenProtector.Protect(value),
				onNone: () => null);

			var update = Builders<GoogleIntegrationConnectionDocument>.Update
				.Set(x => x.GoogleSubject, googleSubject)
				.Set(x => x.Provider, IntegrationProvider.GoogleKeep.ToString())
				.Set(x => x.GoogleEmail, tokenValue.Email.GetValueOrNull())
				.Set(x => x.EncryptedAccessToken, tokenProtector.Protect(tokenValue.AccessToken))
				.Set(x => x.EncryptedRefreshToken, encryptedRefreshToken)
				.Set(x => x.AccessTokenExpiresAtUtc, tokenValue.ExpiresAtUtc.GetValueOrNull())
				.Set(x => x.Scopes, tokenValue.Scopes.ToList())
				.Set(x => x.Capability, capability.ToString())
				.Set(x => x.UpdatedAt, now)
				.Set(x => x.DisconnectedAtUtc, null)
				.SetOnInsert(x => x.UserId, command.UserId)
				.SetOnInsert(x => x.CreatedAt, now);

			var options = new FindOneAndUpdateOptions<GoogleIntegrationConnectionDocument>
			{
				IsUpsert = true,
				ReturnDocument = ReturnDocument.After
			};

			var updated = await collection.FindOneAndUpdateAsync(
				x => x.UserId == command.UserId && x.Provider == IntegrationProvider.GoogleKeep.ToString(),
				update,
				options,
				cancellationToken);

			if (updated is null)
				return Result<GoogleKeepConnectionStatus>.Failure(new Error(ErrorCodes.DatabaseError, "Failed to persist Google Keep connection."));

			return Result<GoogleKeepConnectionStatus>.Success(new GoogleKeepConnectionStatus(
				IsConnected: true,
				Provider: IntegrationProvider.GoogleKeep,
				Capability: capability,
				ConnectedEmail: Option<string>.From(updated.GoogleEmail),
				ConnectedAtUtc: Option<DateTime>.From(updated.CreatedAt)));
		}
		catch (Exception ex)
		{
			return Result<GoogleKeepConnectionStatus>.Failure(new Error(
				ErrorCodes.DatabaseError,
				"Failed to complete Google Keep connection.",
				ex));
		}
	}
}
