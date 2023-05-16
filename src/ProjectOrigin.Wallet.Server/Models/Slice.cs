using System;

namespace ProjectOrigin.Wallet.Server.Models
{
    public class Slice
    {
        public Guid Id { get; }
        public Guid WalletSectionId { get; }
        public int SectionPosition { get; }
        public Guid CertificateId { get; }
        public long Quantity { get; }
        public long RandomR { get; }
        public bool Verified { get; }

        public Slice(Guid id, Guid walletSectionId, int sectionPosition, Guid certificateId, long quantity, long randomR)
        {
            Id = id;
            WalletSectionId = walletSectionId;
            SectionPosition = sectionPosition;
            CertificateId = certificateId;
            Quantity = quantity;
            RandomR = randomR;
            Verified = false;
        }
    }
}
