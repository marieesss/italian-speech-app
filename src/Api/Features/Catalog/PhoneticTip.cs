using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Catalog;

/// <summary>
/// Conseil rédigé à l'avance, en français, associé à un code de piège phonétique.
/// <para>
/// C'est du <b>contenu</b>, pas du code : la table est éditable en base sans redéploiement.
/// Quand Azure rend un score faible sur un phonème, le conseil correspondant est retrouvé
/// ici — donc gratuit, instantané et toujours juste. Le LLM ne sert qu'à la mise en forme.
/// </para>
/// </summary>
public class PhoneticTip
{
    /// <summary>Code normalisé, sans argument : <c>double_consonant</c>, <c>gli</c>, <c>rolled_r</c>.</summary>
    public required string Code { get; set; }

    /// <summary>Libellé court, affichable en étiquette dans l'interface.</summary>
    public required string LabelFr { get; set; }

    /// <summary>Le conseil lui-même, en français, adressé à l'apprenante.</summary>
    public required string AdviceFr { get; set; }

    /// <summary>
    /// Symboles de phonèmes rendus par Azure qui déclenchent ce conseil quand leur score est faible.
    /// Vide = le conseil n'est servi que via l'annotation <c>phoneticTraps</c> de la phrase.
    /// </summary>
    public List<string> PhonemeSymbols { get; set; } = [];

    public int DisplayOrder { get; set; }
}

public class PhoneticTipConfiguration : IEntityTypeConfiguration<PhoneticTip>
{
    public void Configure(EntityTypeBuilder<PhoneticTip> builder)
    {
        builder.ToTable("PhoneticTips");
        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code).HasMaxLength(64);
        builder.Property(x => x.LabelFr).HasMaxLength(128);
        builder.Property(x => x.AdviceFr).HasMaxLength(1024);
        builder.Property(x => x.PhonemeSymbols).HasJsonbConversion();
    }
}
