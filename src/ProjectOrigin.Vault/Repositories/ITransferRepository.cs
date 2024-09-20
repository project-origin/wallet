using System;
using System.Threading.Tasks;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.ViewModels;

namespace ProjectOrigin.Vault.Repositories;

public interface ITransferRepository
{
    Task InsertTransferredSlice(TransferredSlice newSlice);
    Task<TransferredSlice> GetTransferredSlice(Guid sliceId);
    Task SetTransferredSliceState(Guid sliceId, TransferredSliceState state);
    Task<PageResult<TransferViewModel>> QueryTransfers(QueryTransfersFilter filter);
    Task<PageResultCursor<TransferViewModel>> QueryTransfers(QueryTransfersFilterCursor filter);
    Task<PageResult<AggregatedTransferViewModel>> QueryAggregatedTransfers(QueryAggregatedTransfersFilter filter);
}
