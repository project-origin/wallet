using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.Vault.Options;
public class JobOptions : IValidatableObject
{
    public const string Job = nameof(Job);

    [Required]
    public int CheckForWithdrawnCertificatesIntervalInSeconds { get; set; }
    [Required]
    public int ExpireCertificatesIntervalInSeconds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        List<ValidationResult> results = new();
        if (CheckForWithdrawnCertificatesIntervalInSeconds <= 0)
        {
            results.Add(new ValidationResult("CheckForWithdrawnCertificatesIntervalInSeconds must be greater than 0"));
        }
        if (ExpireCertificatesIntervalInSeconds <= 0)
        {
            results.Add(new ValidationResult("ExpireCertificatesIntervalInSeconds must be greater than 0"));
        }

        return results;
    }
}
