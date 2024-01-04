using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProjectOrigin.WalletSystem.Server.Serialization;

public class IHDPublicKeyConverter : JsonConverter<IHDPublicKey>
{
    private readonly IHDAlgorithm _algorithm;

    public IHDPublicKeyConverter(IHDAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }

    public override IHDPublicKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _algorithm.ImportHDPublicKey(reader.GetBytesFromBase64());
    }

    public override void Write(Utf8JsonWriter writer, IHDPublicKey value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.Export());
    }
}

public class IHDPublicKeySchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(IHDPublicKey))
        {
            schema.Type = "string";
        }
    }
}
