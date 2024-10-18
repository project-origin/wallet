using FluentAssertions;
using ProjectOrigin.Vault.Options;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Options;

public class NetworkOptionsTests
{
    [Fact]
    public void ToStringTest()
    {
        var options = new NetworkOptions();
        options.Registries.Add("registry1", new RegistryInfo { Url = "http://registry1" });
        options.Registries.Add("registry2", new RegistryInfo { Url = "http://registry2" });
        options.Areas.Add("area1", new AreaInfo { IssuerKeys = new[] { new KeyInfo { PublicKey = "key1" } } });
        options.Areas.Add("area2", new AreaInfo { IssuerKeys = new[] { new KeyInfo { PublicKey = "key2" } } });
        options.Issuers.Add("issuer1", new IssuerInfo { StampUrl = "http://stamp1" });
        options.Issuers.Add("issuer2", new IssuerInfo { StampUrl = "http://stamp2" });

        var str = options.ToString();

        str.Should().Contain("Registries found: ");
    }
}
