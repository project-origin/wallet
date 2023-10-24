using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using ProjectOrigin.Common.V1;

namespace ProjectOrigin.WalletSystem.Server.Models;

public record CertificateViewModel
{
    public required Guid Id { get; init; }
    public required string RegistryName { get; init; } = string.Empty;
    public required GranularCertificateType CertificateType { get; init; }
    public required string GridArea { get; init; } = string.Empty;
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public List<CertificateAttribute> Attributes { get; } = new();
    public List<SliceViewModel> Slices { get; } = new();

    public V1.GranularCertificate ToProto()
    {
        var fedId = new FederatedStreamId
        {
            Registry = RegistryName,
            StreamId = new Common.V1.Uuid
            {
                Value = Id.ToString()
            }
        };

        var res = new V1.GranularCertificate
        {
            FederatedId = fedId,
            Quantity = (uint)Slices.Sum(x => x.Quantity),
            End = Timestamp.FromDateTimeOffset(EndDate),
            Start = Timestamp.FromDateTimeOffset(StartDate),
            GridArea = GridArea,
            Type = CertificateType.ToProto(),
            Attributes = { Attributes.Select(att => att.ToProto()) }
        };

        return res;
    }
}
