using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Database.Postgres;

public sealed class PostgresOptions
{
    [Required]
    public required string ConnectionString { get; set; }
}
