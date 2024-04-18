using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class ServiceOptions
{
    [Required(AllowEmptyStrings = false)]
    public required Uri EndpointAddress { get; set; }

    [Required(AllowEmptyStrings = false)]
    public PathString PathBase { get; set; } = string.Empty;
}
