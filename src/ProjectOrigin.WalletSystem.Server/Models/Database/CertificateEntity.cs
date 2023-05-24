using System;

namespace ProjectOrigin.WalletSystem.Server.Models.Database;

public class CertificateEntity
{
    public Guid Id { get; set; }
    public string Registry { get; set; }
    public long Quantity { get; set; }
}
