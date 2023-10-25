using System;
using System.Collections.Generic;
using System.Linq;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

public static class MapperV1
{
    public static CertificateType MapToV1(this GranularCertificateType granularCertificateType) =>
        granularCertificateType switch
        {
            GranularCertificateType.Consumption => CertificateType.Consumption,
            GranularCertificateType.Production => CertificateType.Production,
            _ => throw new ArgumentOutOfRangeException(nameof(granularCertificateType), granularCertificateType, null)
        };

    //TODO: Key to be camelcase???
    public static Dictionary<string, string> MapToV1(this List<CertificateAttribute> attributes) =>
        attributes
            .OrderBy(a => a.Key)
            .ToDictionary(a => a.Key, a => a.Value);
}
