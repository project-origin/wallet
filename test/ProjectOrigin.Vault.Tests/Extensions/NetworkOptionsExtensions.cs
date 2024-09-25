using ProjectOrigin.TestCommon;
using ProjectOrigin.Vault.Options;
using YamlDotNet.Serialization;

namespace ProjectOrigin.Vault.Tests;

public static class NetworkOptionsExtensions
{
    public static string ToTempYamlFileUri(this NetworkOptions networkOptions)
    {
        var serializer = new SerializerBuilder()
           .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
           .Build();
        var yaml = serializer.Serialize(networkOptions);
        return "file://" + TempFile.WriteAllText(yaml, ".yaml");
    }
}
