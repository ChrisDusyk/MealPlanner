using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Commands;

/// <summary>
/// Admin command to create a curated tag.
/// </summary>
public record CreateCatalogueTagCommand(string Slug, string Name) : ICommand<CatalogueTag>;

public class CreateCatalogueTagCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<CreateCatalogueTagCommand, CatalogueTag>
{
	public async Task<Result<CatalogueTag>> HandleAsync(
		CreateCatalogueTagCommand command,
		CancellationToken cancellationToken = default)
	{
		var (slug, validation) = NormalizeAndValidate(command.Slug, command.Name);
		if (validation is not null)
		{
			return Result<CatalogueTag>.Failure(validation);
		}

		try
		{
			var existing = await db.CatalogueTags
				.AsNoTracking()
				.AnyAsync(t => t.Slug == slug, cancellationToken);

			if (existing)
			{
				return Result<CatalogueTag>.Failure(
					new Error(ErrorCodes.ValidationFailed, "A tag with this slug already exists."));
			}

			var entity = new CatalogueTagEntity
			{
				Id = Guid.NewGuid(),
				Slug = slug,
				Name = command.Name.Trim(),
				CreatedAt = DateTime.UtcNow
			};

			db.CatalogueTags.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<CatalogueTag>.Success(CatalogueMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<CatalogueTag>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to create tag.", ex));
		}
	}

	internal static (string Slug, Error? Error) NormalizeAndValidate(string slug, string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return ("", new Error(ErrorCodes.ValidationFailed, "Tag name is required."));
		}
		if (string.IsNullOrWhiteSpace(slug))
		{
			return ("", new Error(ErrorCodes.ValidationFailed, "Tag slug is required."));
		}

		var normalized = slug.Trim().ToLowerInvariant();
		// Slugs must be alphanumeric + dashes. Reject obvious junk.
		foreach (var ch in normalized)
		{
			if (!(char.IsLetterOrDigit(ch) || ch == '-'))
			{
				return ("", new Error(
					ErrorCodes.ValidationFailed,
					"Tag slug may only contain letters, digits, and dashes."));
			}
		}

		return (normalized, null);
	}
}

/// <summary>
/// Admin command to update a curated tag.
/// </summary>
public record UpdateCatalogueTagCommand(Guid Id, string Slug, string Name) : ICommand<CatalogueTag>;

public class UpdateCatalogueTagCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateCatalogueTagCommand, CatalogueTag>
{
	public async Task<Result<CatalogueTag>> HandleAsync(
		UpdateCatalogueTagCommand command,
		CancellationToken cancellationToken = default)
	{
		var (slug, validation) = CreateCatalogueTagCommandHandler
			.NormalizeAndValidate(command.Slug, command.Name);
		if (validation is not null)
		{
			return Result<CatalogueTag>.Failure(validation);
		}

		try
		{
			var entity = await db.CatalogueTags
				.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);

			if (entity is null)
			{
				return Result<CatalogueTag>.Failure(
					new Error(ErrorCodes.NotFound, "Tag was not found."));
			}

			var slugCollision = await db.CatalogueTags
				.AsNoTracking()
				.AnyAsync(t => t.Slug == slug && t.Id != command.Id, cancellationToken);
			if (slugCollision)
			{
				return Result<CatalogueTag>.Failure(
					new Error(ErrorCodes.ValidationFailed, "A tag with this slug already exists."));
			}

			entity.Slug = slug;
			entity.Name = command.Name.Trim();
			await db.SaveChangesAsync(cancellationToken);

			return Result<CatalogueTag>.Success(CatalogueMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<CatalogueTag>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update tag.", ex));
		}
	}
}

/// <summary>
/// Admin command to delete a curated tag. Fails if the tag is in use.
/// </summary>
public record DeleteCatalogueTagCommand(Guid Id) : ICommand<Unit>;

public class DeleteCatalogueTagCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<DeleteCatalogueTagCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DeleteCatalogueTagCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entity = await db.CatalogueTags
				.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);

			if (entity is null)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Tag was not found."));
			}

			var inUse = await db.CatalogueRecipeTags
				.AsNoTracking()
				.AnyAsync(rt => rt.TagId == command.Id, cancellationToken);
			if (inUse)
			{
				return Result<Unit>.Failure(
					new Error(
						ErrorCodes.ValidationFailed,
						"This tag is in use and cannot be deleted. Remove it from recipes first."));
			}

			db.CatalogueTags.Remove(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to delete tag.", ex));
		}
	}
}
