using System;
using System.Collections.Generic;
using System.Text;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class RegistryOptions
{
    public Dictionary<string, string> RegistryUrls { get; set; } = new Dictionary<string, string>();
    public byte[] Dk1IssuerPrivateKeyPem { get; set; } = Array.Empty<byte>();
    public byte[] Dk2IssuerPrivateKeyPem { get; set; } = Array.Empty<byte>();
    public IPrivateKey Dk1IssuerKey => new Ed25519Algorithm().ImportPrivateKeyText(Encoding.UTF8.GetString(Dk1IssuerPrivateKeyPem));

}
