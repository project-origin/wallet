namespace ProjectOrigin.WalletSystem.Server.Models;

public enum SliceState
{
    /// <summary>
    /// The slice has been verified but is invalid.
    /// other -1xx codes can be added for different invalid states.
    /// </summary>
    Invalid = -100,

    /// <summary>
    /// The slice has not been verified yet.
    /// </summary>
    Unverified = 0,

    /// <summary>
    /// The slice has been verified and is valid.
    /// </summary>
    Valid = 100,
}
