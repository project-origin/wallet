using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using ProjectOrigin.Registry.V1;

namespace ProjectOrigin.Vault.Serialization;

public class TransactionConverter : JsonConverter<Transaction>
{
    public override Transaction? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Transaction.Parser.ParseFrom(reader.GetBytesFromBase64());
    }

    public override void Write(Utf8JsonWriter writer, Transaction value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.ToByteArray());
    }
}
