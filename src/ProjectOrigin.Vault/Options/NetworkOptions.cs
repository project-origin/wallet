using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ProjectOrigin.Vault.Options;

public record NetworkOptions : IValidatableObject
{
    public IDictionary<string, RegistryInfo> Registries { get; init; } = new Dictionary<string, RegistryInfo>();
    public IDictionary<string, AreaInfo> Areas { get; init; } = new Dictionary<string, AreaInfo>();
    public IDictionary<string, IssuerInfo> Issuers { get; init; } = new Dictionary<string, IssuerInfo>();

    [Required]
    public int DaysBeforeCertificatesExpire { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("Registries found: ");
        foreach (var registry in Registries)
        {
            sb.Append($"{registry.Key}-Url:{registry.Value.Url}. ");
        }
        sb.Append("Areas found: ");
        foreach (var area in Areas)
        {
            sb.Append($"{area.Key}-IssuerKeys:{area.Value}. ");
        }
        sb.Append("Issuers found: ");
        foreach (var issuer in Issuers)
        {
            sb.Append($"{issuer.Key}-StampUrl:{issuer.Value.StampUrl}. ");
        }
        return sb.ToString();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        List<ValidationResult> results = new();
        if (DaysBeforeCertificatesExpire <= 0)
        {
            results.Add(new ValidationResult("DaysBeforeCertificatesExpire must be greater than 0"));
        }

        return results;
    }
}

public record RegistryInfo
{
    public required string Url { get; init; }
}

public class AreaInfo
{
    public required IList<KeyInfo> IssuerKeys { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var key in IssuerKeys)
        {
            sb.Append($"{key.PublicKey} ");
        }
        return sb.ToString();

    }
}

public record KeyInfo
{
    public required string PublicKey { get; init; }
}

public record IssuerInfo
{
    public required string StampUrl { get; init; }
}
