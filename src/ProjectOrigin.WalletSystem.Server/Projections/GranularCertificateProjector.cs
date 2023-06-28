using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace ProjectOrigin.WalletSystem.Server.Projections;

public class GranularCertificateProjector : IStreamProjector<GranularCertificate>
{
    private static Lazy<Dictionary<string, MessageDescriptor>> _lazyDescriptorDictionary = new Lazy<Dictionary<string, MessageDescriptor>>(() =>
    {
        return Assembly.GetAssembly(typeof(Electricity.V1.IssuedEvent))!
            .GetTypes()
            .Where(type =>
                type.IsClass
                && typeof(IMessage).IsAssignableFrom(type))
            .Select(type =>
            {
                var descriptor = (MessageDescriptor)type.GetProperty(nameof(IMessage.Descriptor), BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                return descriptor;
            })
            .ToDictionary(descriptor => descriptor.FullName, descriptor => descriptor);
    });

    public GranularCertificate Project(IEnumerable<Registry.V1.Transaction> transactions)
    {
        var issuedEvent = Deserialize(transactions.First().Header.PayloadType, transactions.First().Payload) as Electricity.V1.IssuedEvent;
        if (issuedEvent == null)
            throw new Exception("First event must be an IssuedEvent");

        var granularCertificate = new GranularCertificate(issuedEvent);

        foreach (var transaction in transactions.Skip(1))
        {
            var @event = Deserialize(transaction.Header.PayloadType, transaction.Payload);
            var method = granularCertificate.GetType().GetMethod(nameof(granularCertificate.Apply), new[] { @event.GetType() });
            if (method == null)
                throw new NotSupportedException($"Event type ”{@event.GetType().FullName}” is not supported");
            method.Invoke(this, new[] { @event });
        }

        return granularCertificate;
    }

    private IMessage Deserialize(string type, ByteString content)
    {
        var dictionary = _lazyDescriptorDictionary.Value;
        if (dictionary.TryGetValue(type, out var descriptor))
            return descriptor.Parser.ParseFrom(content);
        else
            throw new NotSupportedException($"Event type ”{type}” is not supported");
    }
}

