using System.Collections.Generic;

namespace ProjectOrigin.Vault.Options;

public record NetworkOptions
{
    public IDictionary<string, RegistryInfo> Registries { get; init; } = new Dictionary<string, RegistryInfo>();
    public IDictionary<string, AreaInfo> Areas { get; init; } = new Dictionary<string, AreaInfo>();
}

public record RegistryInfo
{
    public required string Url { get; init; }
}

public class AreaInfo
{
    public required IList<KeyInfo> IssuerKeys { get; set; }
    public ChroniclerInfo? Chronicler { get; init; }
}

public record KeyInfo
{
    public required string PublicKey { get; init; }
}

public record ChroniclerInfo
{
    public required string Url { get; init; }

    public required IList<KeyInfo> SignerKeys { get; set; } = new List<KeyInfo>();
}
