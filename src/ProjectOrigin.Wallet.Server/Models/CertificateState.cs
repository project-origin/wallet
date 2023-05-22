namespace ProjectOrigin.Wallet.Server.Models;

public enum CertificateState
{
    /// <summary>
    /// The certificate could not be loaded or is invalid.
    /// other -1xx codes can be added for different invalid states.
    /// </summary>
    Invalid = -100,

    /// <summary>
    /// The certificate has been inserted, but header information has not yet been loaded from the registry.
    /// </summary>
    Inserted = 0,

    /// <summary>
    /// The certificate has been loaded from the registry and is valid.
    /// </summary>
    Valid = 100,
}
