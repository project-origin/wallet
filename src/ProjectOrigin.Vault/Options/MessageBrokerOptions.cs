using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.Vault.Options;

public class MessageBrokerOptions : IValidatableObject
{
    public MessageBrokerType Type { get; set; }

    public RabbitMqOptions? RabbitMq { get; set; } = null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        List<ValidationResult> results = new();
        switch (Type)
        {
            case MessageBrokerType.InMemory:
                break;

            case MessageBrokerType.RabbitMq:
                if (RabbitMq is null)
                    results.Add(new ValidationResult($"Not supported message broker type: ”{Type}”"));
                else
                    Validator.TryValidateObject(RabbitMq, new ValidationContext(RabbitMq), results, true);
                break;

            default:
                results.Add(new ValidationResult($"Not supported message broker type: ”{Type}”"));
                break;
        }

        return results;
    }
}
