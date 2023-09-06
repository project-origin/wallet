using System.Threading.Tasks;

namespace ProjectOrigin.WalletSystem.Server.Database;

public interface IRepositoryUpgrader
{
    Task Upgrade();
    Task<bool> IsUpgradeRequired();
}
