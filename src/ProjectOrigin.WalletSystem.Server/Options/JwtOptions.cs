using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class JwtOptions : IValidatableObject
{
    public IEnumerable<JwtIssuer> Issuers { get; init; } = new List<JwtIssuer>();

    public string Audience { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return Issuers.SelectMany(i => i.Validate(new ValidationContext(i)));
    }
}
