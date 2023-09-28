using System;

namespace ProjectOrigin.WalletSystem.Server.Models;

public enum GranularCertificateType
{
    Consumption = 1,
    Production = 2
}

public static class GranularCertificateTypeExtensions
{
    public static V1.GranularCertificateType ToProto(this GranularCertificateType type)
    {
        if (type == GranularCertificateType.Production)
            return V1.GranularCertificateType.Production;

        if (type == GranularCertificateType.Consumption)
            return V1.GranularCertificateType.Consumption;

        throw new ArgumentException("GranularCertificateType not supported. Type: " + type);
    }
}
