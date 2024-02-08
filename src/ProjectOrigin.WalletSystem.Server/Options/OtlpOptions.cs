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
        if (Enabled)
        {
            if (Endpoint == null)
            {
                yield return new ValidationResult(
                    $"The {nameof(Endpoint)} field is required when telemetry is enabled.",
                    new[] { nameof(Endpoint) });
            }
            else
            {
                if (!Uri.IsWellFormedUriString(Endpoint.ToString(), UriKind.Absolute))
                {
                    yield return new ValidationResult(
                        $"The {nameof(Endpoint)} field must be a valid, well-formed URI.",
                        new[] { nameof(Endpoint) });
                }
                else if (Endpoint.Scheme != Uri.UriSchemeHttp && Endpoint.Scheme != Uri.UriSchemeHttps)
                {
                    yield return new ValidationResult(
                        $"The {nameof(Endpoint)} must use the HTTP or HTTPS scheme.",
                        new[] { nameof(Endpoint) });
                }
            }
        }
    }

}
