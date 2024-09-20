using System.Collections.Generic;

namespace ProjectOrigin.Vault.Projections;
public interface IStreamProjector<T>
{
    T Project(IEnumerable<Registry.V1.Transaction> transactions);
}
