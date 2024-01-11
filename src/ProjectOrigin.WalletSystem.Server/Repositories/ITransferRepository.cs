using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public interface ITransferRepository
{
    Task InsertTransferredSlice(TransferredSlice newSlice);
    Task<TransferredSlice> GetTransferredSlice(Guid sliceId);
    Task SetTransferredSliceState(Guid sliceId, TransferredSliceState state);

    Task<IEnumerable<TransferViewModel>> GetTransfers(TransferFilter filter);
}
