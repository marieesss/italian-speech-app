namespace ItalianApp.Api.Features.Identity;

public class AccountOptions
{
    public const string SectionName = "Identity";

    // The app has one intended user. Turn this off once her account exists.
    public bool AllowRegistration { get; set; } = true;

    public int MinimumPasswordLength { get; set; } = 10;
}
