using MealPlanner.Api.Features.Integrations.GoogleKeep.Options;
using MealPlanner.Api.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Services;

public sealed class IntegrationTokenProtector(
	IDataProtectionProvider dataProtectionProvider,
	IOptions<GoogleIntegrationsOptions> options)
	: IIntegrationTokenProtector
{
	private const string DefaultPurpose = "mealplanner-google-integrations";

	private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
		string.IsNullOrWhiteSpace(options.Value.TokenEncryptionKey)
			? DefaultPurpose
			: options.Value.TokenEncryptionKey);

	public string Protect(string plaintext)
	{
		if (string.IsNullOrWhiteSpace(plaintext))
			return string.Empty;

		return _protector.Protect(plaintext);
	}

	public Result<string> Unprotect(string ciphertext)
	{
		if (string.IsNullOrWhiteSpace(ciphertext))
			return Result<string>.Failure(new Error(ErrorCodes.ValidationFailed, "Ciphertext token is required."));

		try
		{
			return Result<string>.Success(_protector.Unprotect(ciphertext));
		}
		catch (Exception ex)
		{
			return Result<string>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Failed to decrypt integration token.",
				ex));
		}
	}
}
