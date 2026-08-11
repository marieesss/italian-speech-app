namespace ItalianApp.Api.Features.Catalog;

/// <summary>
/// Code de piège phonétique annoté sur une phrase, sous la forme <c>code</c> ou <c>code:argument</c>.
/// L'argument précise l'occurrence concernée : <c>double_consonant:tt</c>, <c>stress:prenotàre</c>.
/// </summary>
/// <param name="Code">Code normalisé, clé de la table de conseils.</param>
/// <param name="Argument">Précision facultative, affichée telle quelle dans le conseil.</param>
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
