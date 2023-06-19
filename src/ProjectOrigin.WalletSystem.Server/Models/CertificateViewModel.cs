using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Register.V1;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Models;

public class CertificateViewModel
{
    public Guid Id { get; set; }
    public string Registry { get; set; } = string.Empty;
    public GranularCertificateType CertificateType { get; set; }
    public string GridArea { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public List<CertificateAttribute> Attributes { get; set; } = new ();
    public List<SliceViewModel> Slices { get; set; } = new ();

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
        if(type == GranularCertificateType.Production)
            return V1.GranularCertificateType.Production;

        if(type == GranularCertificateType.Consumption)
            return V1.GranularCertificateType.Consumption;

        if(type == GranularCertificateType.Invalid)
            return V1.GranularCertificateType.Invalid;

        throw new ArgumentException("GranularCertificateType not supported. Type: " + type);
    }
}
