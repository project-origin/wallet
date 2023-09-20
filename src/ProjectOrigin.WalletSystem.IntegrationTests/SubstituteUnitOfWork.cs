using System;
using System.Collections.Concurrent;
using AutoFixture;
using NSubstitute;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;

public class SubstituteUnitOfWork
{
    private readonly Fixture _fixture;
    private readonly ICertificateRepository _certificateRepository;
    private readonly IWalletRepository _walletRepository;

    private ConcurrentDictionary<Guid, int> _sequences = new();

    public SubstituteUnitOfWork()
    {
        _fixture = new Fixture();
        _certificateRepository = Substitute.For<ICertificateRepository>();
        _walletRepository = Substitute.For<IWalletRepository>();

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.WalletRepository.Returns(_walletRepository);
        unitOfWork.CertificateRepository.Returns(_certificateRepository);

        _walletRepository.GetNextNumberForId(Arg.Any<Guid>()).Returns(x => GetNextNumberForId(x.Arg<Guid>()));
    }

    private int GetNextNumberForId(Guid id)
    {
        return _sequences.AddOrUpdate(id, 1, (key, value) => value + 1);
    }

    public Wallet CreateWallet()
    {
        var key = new Secp256k1Algorithm().GenerateNewPrivateKey();
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            Owner = _fixture.Create<string>(),
            PrivateKey = key
        };

        _walletRepository.GetWallet(wallet.Id).Returns(wallet);
        _walletRepository.GetWalletByOwner(wallet.Owner).Returns(wallet);

        var remainderEndpoint = CreateDepositEndpoint(wallet);
        _walletRepository.GetWalletRemainderDepositEndpoint(wallet.Id).Returns(remainderEndpoint);

        return wallet;
    }

    public DepositEndpoint CreateDepositEndpoint(Wallet wallet)
    {
        var position = GetNextNumberForId(wallet.Id);
        var depositEndpoint = new DepositEndpoint
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            WalletPosition = position,
            PublicKey = wallet.PrivateKey.Derive(position).Neuter(),
            Owner = string.Empty,
            ReferenceText = string.Empty,
            Endpoint = string.Empty
        };
        _walletRepository.GetDepositEndpoint(depositEndpoint.Id).Returns(depositEndpoint);
        _walletRepository.GetDepositEndpointFromPublicKey(depositEndpoint.PublicKey).Returns(depositEndpoint);
        return depositEndpoint;
    }

    public Certificate CreateCertificate()
    {
        throw new NotImplementedException();
    }

    internal (Certificate, Slice) AddCertificate(DepositEndpoint depositEndpoint, GranularCertificateType type, int quantity)
    {
        throw new NotImplementedException();
    }
}
