using System.Data;
using Dapper;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Database.Mapping;

public class HDPublicKeyTypeHandler : SqlMapper.TypeHandler<IHDPublicKey>
{
    private readonly IHDAlgorithm _algorithm;

    public HDPublicKeyTypeHandler(IHDAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }

    public override IHDPublicKey Parse(object value)
    {
        return _algorithm.ImportHDPublicKey(value as byte[]);
    }

    public override void SetValue(IDbDataParameter parameter, IHDPublicKey value)
    {
        parameter.Value = value.Export().ToArray();
    }
}
