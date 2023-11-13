using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ProjectOrigin.Common.V1;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;
using ProjectOrigin.WalletSystem.V1;

namespace ProjectOrigin.WalletSystem.Server.Services.GRPC;

[Authorize]
public class WalletService : V1.WalletService.WalletServiceBase
{
    private readonly string _walletSystemAddress;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHDAlgorithm _hdAlgorithm;
    private readonly IBus _bus;

    public WalletService(IUnitOfWork unitOfWork, IHDAlgorithm hdAlgorithm, IOptions<ServiceOptions> options, IBus bus)
    {
        _walletSystemAddress = options.Value.EndpointAddress;
        _unitOfWork = unitOfWork;
        _hdAlgorithm = hdAlgorithm;
        _bus = bus;
    }

    public override async Task<V1.CreateWalletDepositEndpointResponse> CreateWalletDepositEndpoint(V1.CreateWalletDepositEndpointRequest request, ServerCallContext context)
    {
        var subject = context.GetSubject();

        var wallet = await _unitOfWork.WalletRepository.GetWallet(subject);

        if (wallet is null)
        {
            var key = _hdAlgorithm.GenerateNewPrivateKey();
            wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = subject,
                PrivateKey = key
            };

            await _unitOfWork.WalletRepository.Create(wallet);
        }

        var endpoint = await _unitOfWork.WalletRepository.CreateWalletEndpoint(wallet.Id);
        _unitOfWork.Commit();

        return new V1.CreateWalletDepositEndpointResponse
        {
            WalletDepositEndpoint = new V1.WalletDepositEndpoint()
            {
                Version = 1,
                Endpoint = _walletSystemAddress,
                PublicKey = ByteString.CopyFrom(endpoint.PublicKey.Export())
            }
        };
    }

    public override async Task<QueryResponse> QueryGranularCertificates(QueryRequest request, ServerCallContext context)
    {
        var subject = context.GetSubject();

        var certificates = await _unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject, new CertificatesFilter
        {
            Start = request.Filter?.Start.ToNullableDateTimeOffset(),
            End = request.Filter?.End.ToNullableDateTimeOffset(),
        });

        var response = new QueryResponse();
        foreach (var gc in certificates)
        {
            response.GranularCertificates.Add(gc.ToProto());
        }

        return response;
    }

    public override async Task<CreateReceiverDepositEndpointResponse> CreateReceiverDepositEndpoint(CreateReceiverDepositEndpointRequest request, ServerCallContext context)
    {
        var subject = context.GetSubject();
        var ownerPublicKey = new Secp256k1Algorithm().ImportHDPublicKey(request.WalletDepositEndpoint.PublicKey.Span);

        var foundEndpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(ownerPublicKey);
        if (foundEndpoint is not null)
        {
            var wallet = await _unitOfWork.WalletRepository.GetWallet(foundEndpoint.WalletId);
            if (wallet.Owner == subject)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Cannot create receiver deposit endpoint to self."));
            }
        }

        var receiverDepositEndpoint = await _unitOfWork.WalletRepository.CreateExternalEndpoint(subject, ownerPublicKey, request.Reference, request.WalletDepositEndpoint.Endpoint);
        _unitOfWork.Commit();

        return new V1.CreateReceiverDepositEndpointResponse
        {
            ReceiverId = new Uuid
            {
                Value = receiverDepositEndpoint.Id.ToString()
            }
        };
    }

    public override Task<TransferResponse> TransferCertificate(TransferRequest request, ServerCallContext context)
    {
        var owner = context.GetSubject();
        var command = new TransferCertificateCommand
        {
            Owner = owner,
            Registry = request.CertificateId.Registry,
            CertificateId = new Guid(request.CertificateId.StreamId.Value),
            Quantity = request.Quantity,
            Receiver = new Guid(request.ReceiverId.Value),
            HashedAttributes = request.HashedAttributes.ToArray(),
        };

        _bus.Publish(command);

        return Task.FromResult(new TransferResponse());
    }

    public override Task<ClaimResponse> ClaimCertificates(ClaimRequest request, ServerCallContext context)
    {
        var owner = context.GetSubject();

        var command = new ClaimCertificateCommand
        {
            Owner = owner,
            ClaimId = Guid.NewGuid(),
            ConsumptionRegistry = request.ConsumptionCertificateId.Registry,
            ConsumptionCertificateId = Guid.Parse(request.ConsumptionCertificateId.StreamId.Value),
            ProductionRegistry = request.ProductionCertificateId.Registry,
            ProductionCertificateId = Guid.Parse(request.ProductionCertificateId.StreamId.Value),
            Quantity = request.Quantity,
        };

        _bus.Publish(command);

        return Task.FromResult(new ClaimResponse()
        {
            ClaimId = new Uuid
            {
                Value = command.ClaimId.ToString()
            }
        });
    }

    public override async Task<ClaimQueryResponse> QueryClaims(ClaimQueryRequest request, ServerCallContext context)
    {
        var owner = context.GetSubject();

        var claims = await _unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter()
        {
            Start = request.Filter?.Start.ToNullableDateTimeOffset(),
            End = request.Filter?.End.ToNullableDateTimeOffset(),
        });

        return new ClaimQueryResponse
        {
            Claims = { claims.Select(c => c.ToProto()) }
        };
    }
}
