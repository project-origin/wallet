using System.IO;
using ProjectOrigin.TestCommon;
using ProjectOrigin.Vault.Options;
using YamlDotNet.Serialization;

namespace ProjectOrigin.Vault.Tests.Extensions;

public static class NetworkOptionsExtensions
{
    public static string ToTempYamlFileUri(this NetworkOptions networkOptions)
    {
        var serializer = new SerializerBuilder()
           .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
           .Build();
        var yaml = serializer.Serialize(networkOptions);
        var path = "file://" + TempFile.WriteAllText(yaml, ".yaml");
        return path;
    }

    public static string ToTempYamlFile(this NetworkOptions networkOptions)
    {
        var configFile = Path.GetTempFileName() + ".yaml";
        var serializer = new SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(networkOptions);
        File.WriteAllText(configFile, yaml);
        return configFile;
    }
}
