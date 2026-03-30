using System.Security.Claims;
using MealPlanner.Api.Features.Auth;

namespace MealPlanner.Api.Features.Admin;

public static class AdminEndpoints
{
	public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/admin")
			.WithTags("Admin")
			.RequireAuthorization(RbacAuthorization.RequireAdminRolePolicy);

		group.MapGet("/ping", Ping);

		return app;
	}

	private static IResult Ping(HttpContext httpContext)
	{
		var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
			?? httpContext.User.FindFirst("sub")?.Value
			?? string.Empty;

		return Results.Ok(new
		{
			message = "Admin access granted.",
			userId,
			timestampUtc = DateTime.UtcNow
		});
	}
}
