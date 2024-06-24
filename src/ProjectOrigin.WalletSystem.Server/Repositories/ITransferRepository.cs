using System;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ITransferRepository
{
    Task InsertTransferredSlice(TransferredSlice newSlice);
    Task<TransferredSlice> GetTransferredSlice(Guid sliceId);
    Task SetTransferredSliceState(Guid sliceId, TransferredSliceState state);
    Task<PageResult<TransferViewModel>> QueryTransfers(QueryTransfersFilter filter);
    Task<PageResult<AggregatedTransferViewModel>> QueryAggregatedTransfers(QueryAggregatedTransfersFilter filter);
}
