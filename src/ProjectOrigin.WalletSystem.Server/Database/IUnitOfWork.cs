using ProjectOrigin.WalletSystem.Server.Repositories;

namespace ProjectOrigin.WalletSystem.Server.Database;

public interface IUnitOfWork
{
    void Commit();
    void Rollback();

    IWalletRepository WalletRepository { get; }
    ICertificateRepository CertificateRepository { get; }
    ITransferRepository TransferRepository { get; }
    IClaimRepository ClaimRepository { get; }
    IRequestStatusRepository RequestStatusRepository { get; }
}
