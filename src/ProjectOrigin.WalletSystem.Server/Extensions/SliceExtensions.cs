using ProjectOrigin.Common.V1;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class SliceExtensions
{
    public static FederatedStreamId GetFederatedStreamId(this Slice slice) => new FederatedStreamId { Registry = slice.Registry, StreamId = new Uuid { Value = slice.CertificateId.ToString() } };
}
