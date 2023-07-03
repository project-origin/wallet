using System.Collections.Generic;

namespace ProjectOrigin.WalletSystem.Server.Projections;
public interface IStreamProjector<T>
{
    T Project(IEnumerable<Registry.V1.Transaction> transactions);
}
