using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

public record DisconnectGoogleKeepConnectionCommand(string UserId) : ICommand<Unit>;

public class DisconnectGoogleKeepConnectionCommandHandler(
	MealPlannerDbContext db,
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
			var current = await db.GoogleIntegrationConnections.FirstOrDefaultAsync(c =>
				c.UserId == command.UserId
				&& c.Provider == IntegrationProvider.GoogleKeep.ToString()
				&& c.DisconnectedAtUtc == null, cancellationToken);

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

			current.EncryptedAccessToken = string.Empty;
			current.EncryptedRefreshToken = null;
			current.AccessTokenExpiresAtUtc = null;
			current.DisconnectedAtUtc = DateTime.UtcNow;
			current.UpdatedAt = DateTime.UtcNow;
			current.Capability = IntegrationCapability.Unknown.ToString();

			await db.SaveChangesAsync(cancellationToken);

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
