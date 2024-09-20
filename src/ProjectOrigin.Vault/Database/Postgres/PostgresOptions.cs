using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.Vault.Database.Postgres;

public sealed class PostgresOptions
{
    [Required]
    public required string ConnectionString { get; set; }
}
