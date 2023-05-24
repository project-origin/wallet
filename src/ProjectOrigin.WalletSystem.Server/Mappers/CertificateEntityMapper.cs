using ProjectOrigin.Register.V1;
using ProjectOrigin.WalletSystem.Server.Models.Database;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Mappers
{
    public static class CertificateEntityMapper
    {
        public static GranularCertificate ToDto(CertificateEntity certificate)
        {
            var fedId = new FederatedStreamId
            {
                Registry = certificate.Registry,
                StreamId = new Register.V1.Uuid
                {
                    Value = certificate.Id.ToString()
                }
            };

            return new GranularCertificate
            {
                FederatedId = fedId,
                Quantity = (uint)certificate.Quantity
            };
        }
    }
}
