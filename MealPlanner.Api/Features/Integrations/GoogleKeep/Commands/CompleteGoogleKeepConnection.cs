using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Queries;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

public record CompleteGoogleKeepConnectionCommand(string UserId, string AuthorizationCode, string State)
	: ICommand<GoogleKeepConnectionStatus>;

public class CompleteGoogleKeepConnectionCommandHandler(
	MealPlannerDbContext db,
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
			? capabilityResult.Value
			: IntegrationCapability.Unknown;

		try
		{
			var now = DateTime.UtcNow;
			var tokenValue = tokenResult.Value;
			var googleSubject = tokenValue.GoogleSubject.GetValueOrDefault(command.UserId);
			var encryptedRefreshToken = tokenValue.RefreshToken.Match(
				onSome: value => (string?)tokenProtector.Protect(value),
				onNone: () => null);

			var updated = await db.GoogleIntegrationConnections
				.FirstOrDefaultAsync(x => x.UserId == command.UserId && x.Provider == IntegrationProvider.GoogleKeep.ToString(), cancellationToken);

			if (updated is null)
			{
				updated = new GoogleIntegrationConnectionEntity
				{
					Id = Guid.NewGuid(),
					UserId = command.UserId,
					CreatedAt = now,
					Provider = IntegrationProvider.GoogleKeep.ToString()
				};
				db.GoogleIntegrationConnections.Add(updated);
			}

			updated.GoogleSubject = googleSubject;
			updated.GoogleEmail = tokenValue.Email.GetValueOrNull();
			updated.EncryptedAccessToken = tokenProtector.Protect(tokenValue.AccessToken);
			updated.EncryptedRefreshToken = encryptedRefreshToken;
			updated.AccessTokenExpiresAtUtc = tokenValue.ExpiresAtUtc.GetValueOrNull();
			updated.Scopes = tokenValue.Scopes.ToList();
			updated.Capability = capability.ToString();
			updated.UpdatedAt = now;
			updated.DisconnectedAtUtc = null;

			await db.SaveChangesAsync(cancellationToken);

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
