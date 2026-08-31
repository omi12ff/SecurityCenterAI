using System.Text;
using Microsoft.Extensions.Options;

namespace SecurityCenterAI.Api.Configuration;

public sealed class JwtOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Key) ||
            Encoding.UTF8.GetByteCount(options.Key) < 32)
        {
            failures.Add(
                "Jwt:Key debe configurarse y contener al menos 32 bytes.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience es obligatorio.");
        }

        if (options.ExpirationMinutes is < 5 or > 1440)
        {
            failures.Add(
                "Jwt:ExpirationMinutes debe estar entre 5 y 1440.");
        }

        if (!environment.IsDevelopment() &&
            options.Key.Contains(
                "development",
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                "La clave JWT de desarrollo no puede utilizarse fuera de Development.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
