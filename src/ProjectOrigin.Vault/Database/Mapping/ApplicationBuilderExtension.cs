using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;

namespace ProjectOrigin.Vault.Database.Mapping;

public static class ApplicationBuilderExtension
{
    public static void ConfigureSqlMappers(this IApplicationBuilder app)
    {
        var algorithm = app.ApplicationServices.GetRequiredService<IHDAlgorithm>();
        ConfigureMappers(algorithm);
    }

    public static void ConfigureMappers(IHDAlgorithm algorithm)
    {
        SqlMapper.AddTypeHandler(new HDPrivateKeyTypeHandler(algorithm));
        SqlMapper.AddTypeHandler(new HDPublicKeyTypeHandler(algorithm));
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
