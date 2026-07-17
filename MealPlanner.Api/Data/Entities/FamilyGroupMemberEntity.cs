namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a family group membership. A user belongs to exactly
/// one family at a time (unique index on <see cref="UserId"/>).
/// </summary>
public class FamilyGroupMemberEntity
{
	public Guid Id { get; set; }

	public Guid FamilyGroupId { get; set; }

	/// <summary>
	/// Auth user id (JWT sub) of the member.
	/// </summary>
	public string UserId { get; set; } = string.Empty;

	public DateTime JoinedAt { get; set; }
}
