namespace ProjectOrigin.Vault.Options;

public class WalletCleanupOptions
{
    public int IntervalHours { get; set; } = 24;

    public int RetentionDays { get; set; } = 365;
}
