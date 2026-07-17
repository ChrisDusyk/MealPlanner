namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a pending invitation to join a family group.
/// Invitations target registered users and are accepted or declined in-app.
/// </summary>
public class FamilyInvitationEntity
{
	public Guid Id { get; set; }

	public Guid FamilyGroupId { get; set; }

	/// <summary>
	/// Auth user id (JWT sub) of the family member who sent the invitation.
	/// </summary>
	public string InviterUserId { get; set; } = string.Empty;

	/// <summary>
	/// Auth user id (JWT sub) of the invited user.
	/// </summary>
	public string InviteeUserId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }
}
