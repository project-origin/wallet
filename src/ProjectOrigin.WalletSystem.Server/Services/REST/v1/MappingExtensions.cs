using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

public static class MappingExtensions
{
    public static CertificateType MapToV1(this GranularCertificateType granularCertificateType) =>
        granularCertificateType switch
        {
            GranularCertificateType.Consumption => CertificateType.Consumption,
            GranularCertificateType.Production => CertificateType.Production,
            _ => throw new ArgumentOutOfRangeException(nameof(granularCertificateType), granularCertificateType, null)
        };

    public static Dictionary<string, string> MapToV1(this List<CertificateAttribute> attributes) =>
        attributes
            .OrderBy(a => a.Key)
            .ToDictionary(a => char.ToLower(a.Key[0]) + a.Key.Substring(1), a => a.Value);

    public static GranularCertificate MapToV1(this CertificateViewModel vm) =>
        new()
        {
            FederatedStreamId = new()
            {
                Registry = vm.RegistryName,
                StreamId = vm.CertificateId
            },
            Quantity = vm.Quantity,
            Start = vm.StartDate.ToUnixTimeSeconds(),
            End = vm.EndDate.ToUnixTimeSeconds(),
            GridArea = vm.GridArea,
            CertificateType = vm.CertificateType.MapToV1(),
            Attributes = vm.Attributes.MapToV1()
        };

    public static Claim MapToV1(this ClaimViewModel vm) =>
        new()
        {
            ClaimId = vm.ClaimId,
            Quantity = vm.Quantity,
            ProductionCertificate = new ClaimedCertificate
            {
                FederatedStreamId = new FederatedStreamId
                {
                    Registry = vm.ProductionRegistryName,
                    StreamId = vm.ProductionCertificateId
                },
                Start = vm.ProductionStart.ToUnixTimeSeconds(),
                End = vm.ProductionEnd.ToUnixTimeSeconds(),
                GridArea = vm.ProductionGridArea,
                Attributes = vm.ProductionAttributes.MapToV1()
            },
            ConsumptionCertificate = new ClaimedCertificate
            {
                FederatedStreamId = new FederatedStreamId
                {
                    Registry = vm.ConsumptionRegistryName,
                    StreamId = vm.ConsumptionCertificateId
                },
                Start = vm.ConsumptionStart.ToUnixTimeSeconds(),
                End = vm.ConsumptionEnd.ToUnixTimeSeconds(),
                GridArea = vm.ConsumptionGridArea,
                Attributes = vm.ConsumptionAttributes.MapToV1()
            }
        };

    public static RequestStatus MapToV1(this RequestStatusState requestStatus) =>
        requestStatus switch
        {
            RequestStatusState.Pending => RequestStatus.Pending,
            RequestStatusState.Completed => RequestStatus.Completed,
            RequestStatusState.Failed => RequestStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(requestStatus), requestStatus, null)
        };
}
