using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class JwtOptions : IValidatableObject
{
    public bool AllowAnyJwtToken { get; init; } = false;
    public string Audience { get; init; } = string.Empty;
    public IEnumerable<JwtIssuer> Issuers { get; init; } = new List<JwtIssuer>();
    public bool EnableScopeValidation { get; init; } = false;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return Issuers.SelectMany(i => i.Validate(new ValidationContext(i)));
    }
}
