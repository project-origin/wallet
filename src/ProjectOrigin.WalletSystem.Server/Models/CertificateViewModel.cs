using System;
using ProjectOrigin.Register.V1;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Models;

public class CertificateViewModel
{
    public Guid Id { get; set; }
    public string Registry { get; set; }
    public long Quantity { get; set; }

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
