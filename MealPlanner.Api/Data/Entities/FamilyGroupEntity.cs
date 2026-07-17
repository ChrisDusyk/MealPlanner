namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a family group. A family owns the meal plans, grocery
/// lists, and recipe library shared by its members.
/// </summary>
public class FamilyGroupEntity
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Auth user id (JWT sub) of the member who owns the family. The owner
	/// manages membership; all members edit content equally.
	/// </summary>
	public string OwnerUserId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
