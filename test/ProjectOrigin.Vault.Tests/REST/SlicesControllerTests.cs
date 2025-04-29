using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Tests.TestExtensions;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Services.REST.v1;
using Xunit;

namespace ProjectOrigin.Vault.Tests;

public class SlicesControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly Secp256k1Algorithm _hdAlgorithm;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;

    public SlicesControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _hdAlgorithm = new Secp256k1Algorithm();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task ReceiveSlice_EndpointNotFound()
    {
        // Arrange
        var controller = new SlicesController();

        // Act
        var result = await controller.ReceiveSlice(
            _unitOfWork,
            _hdAlgorithm,
            _fixture.Create<ReceiveRequest>() with
            {
                PublicKey = _hdAlgorithm.GenerateNewPrivateKey().Neuter().Export().ToArray()
            }
            );

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().Be("Endpoint not found for public key.");
    }

    [Fact]
    public async Task ReceiveSlice_InvalidKey()
    {
        // Arrange
        var controller = new SlicesController();

        // Act
        var result = await controller.ReceiveSlice(
            _unitOfWork,
            _hdAlgorithm,
            _fixture.Create<ReceiveRequest>()
            );

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Invalid public key.");
    }

    [Fact]
    public async Task ReceiveSlice_WhenWalletIsDisabled_BadRequest()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new SlicesController();

        var walletId = Guid.NewGuid();

        await _unitOfWork.WalletRepository.Create(new Wallet
        {
            Id = walletId,
            Owner = subject,
            PrivateKey = _hdAlgorithm.GenerateNewPrivateKey(),
        });

        var endpoint = await _unitOfWork.WalletRepository.CreateWalletEndpoint(walletId);

        _unitOfWork.Commit();
        await _dbFixture.DisableWallet(walletId);

        var request = new ReceiveRequest
        {
            PublicKey = endpoint.PublicKey.Export().ToArray(),
            Position = _fixture.Create<uint>(),
            CertificateId = new FederatedStreamId
            {
                Registry = _fixture.Create<string>(),
                StreamId = Guid.NewGuid(),
            },
            Quantity = _fixture.Create<uint>(),
            RandomR = _fixture.Create<byte[]>(),
            HashedAttributes = new List<HashedAttribute>(){
                new HashedAttribute
                {
                    Key = _fixture.Create<string>(),
                    Value = _fixture.Create<string>(),
                    Salt = _fixture.Create<byte[]>(),
                }
            }
        };

        // Act
        var result = await controller.ReceiveSlice(
            _unitOfWork,
            _hdAlgorithm,
            request
        );

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ReceiveSlice_VerifySliceCommand_AcceptedResult()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var controller = new SlicesController();

        var walletId = Guid.NewGuid();

        await _unitOfWork.WalletRepository.Create(new Wallet
        {
            Id = walletId,
            Owner = subject,
            PrivateKey = _hdAlgorithm.GenerateNewPrivateKey(),
        });

        var endpoint = await _unitOfWork.WalletRepository.CreateWalletEndpoint(walletId);

        var request = new ReceiveRequest
        {
            PublicKey = endpoint.PublicKey.Export().ToArray(),
            Position = _fixture.Create<uint>(),
            CertificateId = new FederatedStreamId
            {
                Registry = _fixture.Create<string>(),
                StreamId = Guid.NewGuid(),
            },
            Quantity = _fixture.Create<uint>(),
            RandomR = _fixture.Create<byte[]>(),
            HashedAttributes = new List<HashedAttribute>(){
                new HashedAttribute
                {
                    Key = _fixture.Create<string>(),
                    Value = _fixture.Create<string>(),
                    Salt = _fixture.Create<byte[]>(),
                }
            }
        };

        // Act
        var result = await controller.ReceiveSlice(
            _unitOfWork,
            _hdAlgorithm,
            request
            );

        // Assert
        result.Result.Should().BeOfType<AcceptedResult>()
          .Which.Value.Should().BeOfType<ReceiveResponse>();

    }
}
