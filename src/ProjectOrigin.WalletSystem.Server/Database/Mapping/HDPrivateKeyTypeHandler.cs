using System.Data;
using Dapper;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.WalletSystem.Server.Database.Mapping;

public class HDPrivateKeyTypeHandler : SqlMapper.TypeHandler<IHDPrivateKey>
{
    private readonly IHDAlgorithm _algorithm;

    public HDPrivateKeyTypeHandler(IHDAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }

    public override IHDPrivateKey Parse(object value)
    {
        return _algorithm.ImportHDPrivateKey((byte[])value);
    }

    public override void SetValue(IDbDataParameter parameter, IHDPrivateKey? value)
    {
        parameter.Value = value?.Export().ToArray();
    }
}
