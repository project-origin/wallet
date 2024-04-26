using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public record HeaderOptions : IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    public required string HeaderName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(HeaderName))
        {
            yield return new ValidationResult("HeaderName is required");
        }
    }
}
