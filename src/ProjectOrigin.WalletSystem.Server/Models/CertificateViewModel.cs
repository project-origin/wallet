using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Common.V1;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificateViewModel
{
    public Guid Id { get; init; }
    public string Registry { get; init; } = string.Empty;
    public GranularCertificateType CertificateType { get; init; }
    public string GridArea { get; init; } = string.Empty;
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public List<CertificateAttribute> Attributes { get; } = new();
    public List<SliceViewModel> Slices { get; } = new();

    public GranularCertificate ToProto()
    {
        var fedId = new FederatedStreamId
        {
            Registry = Registry,
            StreamId = new Common.V1.Uuid
            {
                Value = Id.ToString()
            }
        };

        var res = new GranularCertificate
        {
            FederatedId = fedId,
            Quantity = (uint)Slices.Sum(x => x.Quantity),
            End = Timestamp.FromDateTimeOffset(EndDate),
            Start = Timestamp.FromDateTimeOffset(StartDate),
            GridArea = GridArea,
            Type = ToDto(CertificateType),
        };

        foreach (var atr in Attributes)
        {
            res.Attributes.Add(new V1.Attribute { Key = atr.Key, Value = atr.Value });
        }

        return res;
    }

    private V1.GranularCertificateType ToDto(GranularCertificateType type)
    {
        if (type == GranularCertificateType.Production)
            return V1.GranularCertificateType.Production;

        if (type == GranularCertificateType.Consumption)
            return V1.GranularCertificateType.Consumption;

        throw new ArgumentException("GranularCertificateType not supported. Type: " + type);
    }
}
