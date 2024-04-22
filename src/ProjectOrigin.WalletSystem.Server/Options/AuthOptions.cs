using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public record AuthOptions : IValidatableObject
{
    public AuthType Type { get; init; }
    public JwtOptions? Jwt { get; init; }
    public HeaderOptions? Header { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        List<ValidationResult> results = new();

        switch (Type)
        {
            case AuthType.Jwt:

                if (Jwt is null)
                    results.Add(new ValidationResult("Jwt options are required for Jwt authentication"));
                else
                    Validator.TryValidateObject(Jwt, new ValidationContext(Jwt), results, true);
                break;

            case AuthType.Header:
                if (Header is null)
                    results.Add(new ValidationResult("Header options are required for Header authentication"));
                else
                    Validator.TryValidateObject(Header, new ValidationContext(Header), results, true);
                break;

            default:
                results.Add(new ValidationResult($"Not supported authentication type: ”{Type}”"));
                break;
        }

        return results;
    }
}
