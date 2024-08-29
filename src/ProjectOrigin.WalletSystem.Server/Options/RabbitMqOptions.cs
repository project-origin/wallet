using System.ComponentModel.DataAnnotations;

namespace ProjectOrigin.WalletSystem.Server.Options;

public class RabbitMqOptions
{
    public const string RabbitMq = "RabbitMq";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Required, Range(1, 65535)]
    public ushort Port { get; set; } = 0;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public ushort Replicas { get; set; } = 3;

    public bool Quorum { get; set; } = false;
}
