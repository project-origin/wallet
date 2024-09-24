using ProjectOrigin.TestCommon;
using ProjectOrigin.Vault.Options;
using YamlDotNet.Serialization;

namespace ProjectOrigin.Vault.Tests;

public static class NetworkOptionsExtensions
{
    public static string ToYaml(this NetworkOptions networkOptions)
    {
        var serializer = new SerializerBuilder()
           .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
           .Build();
        return serializer.Serialize(networkOptions);
    }

    public static string ToTempFileUri(this NetworkOptions networkOptions) =>
        "file://" + TempFile.WriteAllText(networkOptions.ToYaml(), ".yaml");

}
