using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public record HeaderOptions : IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Name is required");
        }
    }
}
