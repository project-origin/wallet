using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class ServiceOptions
{
    [Required(AllowEmptyStrings = false)]
    public string EndpointAddress { get; set; } = string.Empty;
}
