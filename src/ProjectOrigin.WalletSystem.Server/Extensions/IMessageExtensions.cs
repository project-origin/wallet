using System;
using System.Security.Cryptography;
using Google.Protobuf;
using ProjectOrigin.Common.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Registry.V1;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class IMessageExtensions
{
    public static Transaction SignRegistryTransaction(this IHDPrivateKey key, FederatedStreamId certificateId, IMessage @event)
    {
        var header = new TransactionHeader
        {
            FederatedStreamId = certificateId,
            PayloadType = @event.Descriptor.FullName,
            PayloadSha512 = ByteString.CopyFrom(SHA512.HashData(@event.ToByteArray())),
            Nonce = Guid.NewGuid().ToString(),
        };

        var transaction = new Transaction
        {
            Header = header,
            HeaderSignature = ByteString.CopyFrom(key.Sign(header.ToByteArray())),
            Payload = @event.ToByteString()
        };

        return transaction;
    }
}
