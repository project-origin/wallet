namespace ProjectOrigin.Vault.Options;

public class WalletCleanupOptions
{
    /// <summary>
    /// Whether the wallet cleanup worker is enabled. Defaults to false for GDPR compliance.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Interval in hours between cleanup runs. Defaults to 24 hours.
    /// </summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// Retention days for disabled wallets. Defaults to 365 days.
    /// </summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>
    /// Whether to log detailed information about deleted wallets.
    /// Set to false for GDPR compliance when audit logging is not implemented.
    /// </summary>
    public bool LogDeletedWalletDetails { get; set; } = false;
}
