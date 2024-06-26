using System.Text.Json.Serialization;

namespace ProjectOrigin.WalletSystem.Server.Options;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthType { Jwt, Header }
