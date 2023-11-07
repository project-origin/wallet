using ProjectOrigin.Common.V1;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Extensions;

public static class SliceExtensions
{
    public static FederatedStreamId GetFederatedStreamId(this AbstractSlice slice) => new FederatedStreamId { Registry = slice.RegistryName, StreamId = new Uuid { Value = slice.CertificateId.ToString() } };
}
