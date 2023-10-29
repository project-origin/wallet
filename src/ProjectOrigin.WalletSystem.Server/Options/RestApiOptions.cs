using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class RestApiOptions
{
    [Required(AllowEmptyStrings = false)]
    public PathString BasePath { get; set; } = "/api";
}
