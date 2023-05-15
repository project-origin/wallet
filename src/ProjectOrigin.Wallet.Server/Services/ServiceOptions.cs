using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.Wallet.Server.Services;

public class ServiceOptions
{
    [Required(AllowEmptyStrings = false)]
    public string EndpointAddress { get; set; } = string.Empty;
}
