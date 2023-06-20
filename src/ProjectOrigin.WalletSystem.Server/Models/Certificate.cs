using System;
using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.Server.Models;

public enum GranularCertificateType
{
    Consumption = 1,
    Production = 2
}

public class Certificate
{
    public Guid Id { get; set; }
    public Guid RegistryId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string GridArea { get; set; } = string.Empty;
    public GranularCertificateType CertificateType { get; set; }
    public List<CertificateAttribute> Attributes { get; set; } = new();

    public Certificate() { }

    public Certificate(Guid id, Guid registryId, DateTimeOffset startDate, DateTimeOffset endDate, string gridArea, GranularCertificateType certificateType, List<CertificateAttribute> attributes)
    {
        Id = id;
        RegistryId = registryId;
        StartDate = startDate;
        EndDate = endDate;
        GridArea = gridArea;
        CertificateType = certificateType;
        Attributes = attributes;
    }
}
