using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public record OtlpOptions : IValidatableObject
{
    public const string Prefix = "Otlp";

    [Required]
    public Uri? ReceiverEndpoint { get; init; }

    [Required]
    public required bool Enabled { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        throw new NotImplementedException();
    }
}
