using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public record OtlpOptions : IValidatableObject
{
    public const string Prefix = "Otlp";

    [Required]
    public Uri? Endpoint { get; init; }

    [Required]
    public required bool Enabled { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Enabled)
        {
            case true when Endpoint == null:
                yield return new ValidationResult(
                    $"The {nameof(Endpoint)} field is required when telemetry is enabled.",
                    new[] { nameof(Endpoint) });
                break;
            case true:
            {
                if (!Uri.IsWellFormedUriString(Endpoint.ToString(), UriKind.Absolute))
                {
                    yield return new ValidationResult(
                        $"The {nameof(Endpoint)} field must be a valid URI.",
                        new[] { nameof(Endpoint) });
                }
                break;
            }
        }
    }
}
