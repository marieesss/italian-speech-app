namespace ItalianApp.Api.Features.Catalog;

// "code" or "code:argument", where the argument pins the occurrence:
// double_consonant:tt, stress:prenotàre.
public readonly record struct PhoneticTrap(string Code, string? Argument)
{
    public static PhoneticTrap Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var separator = raw.IndexOf(':');
        if (separator < 0)
        {
            return new PhoneticTrap(raw.Trim(), null);
        }

        var code = raw[..separator].Trim();
        var argument = raw[(separator + 1)..].Trim();

        return new PhoneticTrap(code, argument.Length == 0 ? null : argument);
    }

    public override string ToString() => Argument is null ? Code : $"{Code}:{Argument}";
}
