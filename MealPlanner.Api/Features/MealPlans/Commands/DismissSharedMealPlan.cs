using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command for the recipient to dismiss a shared meal plan.
/// </summary>
public record DismissSharedMealPlanCommand(
	string RecipientUserId,
	string ShareId
) : ICommand<Unit>;

/// <summary>
/// Handles dismissing a shared meal plan by setting DismissedByRecipient = true.
/// Only the recipient can dismiss.
/// </summary>
public class DismissSharedMealPlanCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<DismissSharedMealPlanCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DismissSharedMealPlanCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.ShareId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Share ID is required."));

		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<MealPlanShareDocument>("shares");

			var filter = Builders<MealPlanShareDocument>.Filter.And(
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.Id, command.ShareId),
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.SharedWithUserId, command.RecipientUserId));

			var update = Builders<MealPlanShareDocument>.Update
				.Set(s => s.DismissedByRecipient, true);

			var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

			if (result.MatchedCount == 0)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Share not found or you are not the recipient."));

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to dismiss shared meal plan.", ex));
		}
	}
}
