using System.ComponentModel.DataAnnotations;

namespace SpaceTrafficWeb.Configuration;

public sealed class ExasolOptions
{
    public const string SectionName = "Exasol";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 8563;

    [Required]
    public string Schema { get; set; } = "SPACE_TRAFFIC";

    [Required]
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    [Required]
    public string OdbcDriver { get; set; } = "EXASOL";

    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
