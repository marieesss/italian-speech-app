namespace ItalianApp.Api.Features.Identity;

public class AccountOptions
{
    public const string SectionName = "Identity";

    // The app has one intended user. Turn this off once her account exists.
    public bool AllowRegistration { get; set; } = true;

    public int MinimumPasswordLength { get; set; } = 10;

    // Comma-separated, so it fits a single environment variable. Empty means nobody.
    public string AdminEmails { get; set; } = string.Empty;

    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && AdminEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(admin => string.Equals(admin, email, StringComparison.OrdinalIgnoreCase));
}
