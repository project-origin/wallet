using System;
using ProjectOrigin.Register.V1;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificateViewModel(Guid Id, string Registry, long Quantity)
{
    public GranularCertificate ToProto()
    {
        var fedId = new FederatedStreamId
        {
            Registry = Registry,
            StreamId = new Register.V1.Uuid
            {
                Value = Id.ToString()
            }
        };

        return new GranularCertificate
        {
            FederatedId = fedId,
            Quantity = (uint)Quantity
        };
    }
}
