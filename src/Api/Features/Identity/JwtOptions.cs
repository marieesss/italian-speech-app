using System.ComponentModel.DataAnnotations;

namespace ItalianApp.Api.Features.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string SigningSecret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "italian-app";

    [Required]
    public string Audience { get; set; } = "italian-app";

    [Range(1, 24 * 30)]
    public int LifetimeHours { get; set; } = 72;
}
