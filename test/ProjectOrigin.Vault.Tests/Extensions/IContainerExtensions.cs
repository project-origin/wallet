using System.Collections.Generic;
using System.Linq;
using Testcontainers.PostgreSql;

namespace ProjectOrigin.Vault.Tests.Extensions;

public static class IContainerExtensions
{
    public static string GetLocalConnectionString(this PostgreSqlContainer container, string networkAlias)
    {
        var connectionProperties = new Dictionary<string, string>
        {
            { "Host", networkAlias },
            {
                "Port",
                ((ushort)5432).ToString()
            },
            { "Database", "postgres" },
            { "Username", "postgres" },
            { "Password", "postgres" }
        };

        return string.Join(";", connectionProperties.Select((KeyValuePair<string, string> property) => string.Join("=", property.Key, property.Value)));
    }
}
