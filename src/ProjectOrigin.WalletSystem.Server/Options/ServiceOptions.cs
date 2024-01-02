using System;
using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class ServiceOptions
{
    [Required(AllowEmptyStrings = false)]
    public required Uri EndpointAddress { get; set; }
}
