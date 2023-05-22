using System;

namespace ProjectOrigin.Wallet.Server.Models.Database;

public class CertificateEntity
{
    public Guid Id { get; set; }
    public Guid RegistryId { get; set; }
    public CertificateState State { get; set; }
    public long Quantity { get; set; }

}
