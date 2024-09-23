using System.Text.Json.Serialization;

namespace ProjectOrigin.Vault.Options;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageBrokerType { InMemory, RabbitMq }
