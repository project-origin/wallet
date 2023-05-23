using System.Data;
using Dapper;
using ProjectOrigin.WalletSystem.Server.HDWallet;

namespace ProjectOrigin.WalletSystem.Server.Database.Mapping;

public class HDPublicKeyTypeHandler : SqlMapper.TypeHandler<IHDPublicKey>
{
    private IHDAlgorithm _algorithm;

    public HDPublicKeyTypeHandler(IHDAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }

    public override IHDPublicKey Parse(object value)
    {
        return _algorithm.ImportPublicKey(value as byte[]);
    }

    public override void SetValue(IDbDataParameter parameter, IHDPublicKey value)
    {
        parameter.Value = value.Export().ToArray();
    }
}
