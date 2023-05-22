using ProjectOrigin.Register.V1;
using ProjectOrigin.Wallet.Server.Models.Database;
using ProjectOrigin.Wallet.V1;

namespace ProjectOrigin.Wallet.Server.Mappers
{
    public static class CertificateEntityMapper
    {
        public static GranularCertificate ToDto(CertificateEntity certificate)
        {
            var fedId = new FederatedStreamId
            {
                Registry = certificate.RegistryId.ToString(),
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
