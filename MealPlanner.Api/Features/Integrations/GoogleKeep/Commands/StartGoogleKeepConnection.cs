using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

public record StartGoogleKeepConnectionCommand(string UserId, string RedirectBaseUri)
	: ICommand<StartGoogleKeepConnectionResult>;

public record StartGoogleKeepConnectionResult(string AuthorizationUrl, string State);

public class StartGoogleKeepConnectionCommandHandler(IGoogleOAuthService googleOAuthService)
	: ICommandHandler<StartGoogleKeepConnectionCommand, StartGoogleKeepConnectionResult>
{
	public async Task<Result<StartGoogleKeepConnectionResult>> HandleAsync(
		StartGoogleKeepConnectionCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.UserId))
		{
			return Result<StartGoogleKeepConnectionResult>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"UserId is required."));
		}

		var startResult = await googleOAuthService.BuildAuthorizationUrlAsync(
			command.UserId,
			command.RedirectBaseUri,
			cancellationToken);

		return startResult.Map(start =>
			new StartGoogleKeepConnectionResult(start.AuthorizationUrl, start.State));
	}
}
