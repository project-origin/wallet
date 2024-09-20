using System.Threading.Tasks;

namespace ProjectOrigin.Vault.Database;

public interface IRepositoryUpgrader
{
    Task Upgrade();
    Task<bool> IsUpgradeRequired();
}
