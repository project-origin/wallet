using System;
using System.Security.Cryptography;
using Google.Protobuf;
using ProjectOrigin.Registry.V1;

namespace ProjectOrigin.Vault.Extensions;

public static class TransactionExtensions
{
    public static string ToShaId(this Transaction transaction)
    {
        return Convert.ToBase64String(SHA256.HashData(transaction.ToByteArray()));
    }
}
