using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.Vault.Options;

public class RetryOptions
{
    public const string Retry = nameof(Retry);

    [Required]
    public int RegistryTransactionStillProcessingRetryCount { get; set; }
    [Required]
    public int RegistryTransactionStillProcessingInitialIntervalSeconds { get; set; }
    [Required]
    public int RegistryTransactionStillProcessingIntervalIncrementSeconds { get; set; }
}
