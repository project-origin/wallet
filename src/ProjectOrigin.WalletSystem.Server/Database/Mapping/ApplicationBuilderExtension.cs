using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ProjectOrigin.WalletSystem.Server.HDWallet;

namespace ProjectOrigin.WalletSystem.Server.Database.Mapping;

public static class ApplicationBuilderExtension
{
    public static void ConfigureSqlMappers(this IApplicationBuilder app)
    {
        var algorithm = app.ApplicationServices.GetRequiredService<IHDAlgorithm>();

        SqlMapper.AddTypeHandler<IHDPrivateKey>(new HDPrivateKeyTypeHandler(algorithm));
        SqlMapper.AddTypeHandler<IHDPublicKey>(new HDPublicKeyTypeHandler(algorithm));
    }
}
