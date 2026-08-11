using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Catalog;

/// <summary>
/// Unité d'entraînement. L'<see cref="Id"/> est stable : il sert de clé pour le fichier
/// audio (<c>/audio/it/{id}.mp3</c>) et pour la progression de l'utilisatrice.
/// </summary>
public class Phrase
{
    public Guid Id { get; set; }
    public Guid ScenarioId { get; set; }

    /// <summary>Phrase cible, celle qui est prononcée et scorée.</summary>
    public required string TextIt { get; set; }

    public required string TextFr { get; set; }

    /// <summary>Mise en situation affichée avant la phrase.</summary>
    public required string ContextFr { get; set; }

    /// <summary>1 à 3.</summary>
    public int Difficulty { get; set; } = 1;

    /// <summary>
    /// Codes de pièges phonétiques, éventuellement paramétrés :
    /// <c>["double_consonant:tt", "stress:prenotàre", "gli"]</c>. Voir <see cref="PhoneticTrap"/>.
    /// </summary>
    public List<string> PhoneticTraps { get; set; } = [];

    /// <summary>Renseigné par <c>seed-audio</c>. Nul tant que le MP3 n'existe pas.</summary>
    public string? AudioUrl { get; set; }

    /// <summary>Voix ayant produit le MP3, pour savoir quoi regénérer si la voix change.</summary>
    public string? TtsVoice { get; set; }

    /// <summary>
    /// Date de validation humaine. Une phrase sans <c>ReviewedAt</c> n'est jamais servie
    /// à l'utilisatrice : la relecture n'est pas optionnelle (cf. README, pipeline de contenu).
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    public Scenario Scenario { get; set; } = null!;
}

public class PhraseConfiguration : IEntityTypeConfiguration<Phrase>
{
    public void Configure(EntityTypeBuilder<Phrase> builder)
    {
        builder.ToTable("Phrases", t =>
            t.HasCheckConstraint("CK_Phrases_Difficulty", "\"Difficulty\" BETWEEN 1 AND 3"));

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Scenario)
               .WithMany(x => x.Phrases)
               .HasForeignKey(x => x.ScenarioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.TextIt).HasMaxLength(512);
        builder.Property(x => x.TextFr).HasMaxLength(512);
        builder.Property(x => x.ContextFr).HasMaxLength(1024);
        builder.Property(x => x.AudioUrl).HasMaxLength(512);
        builder.Property(x => x.TtsVoice).HasMaxLength(64);

        builder.Property(x => x.PhoneticTraps).HasJsonbConversion();

        // Deux phrases identiques dans un même scénario sont une erreur de contenu.
        builder.HasIndex(x => new { x.ScenarioId, x.TextIt }).IsUnique();

        // Sert la file de drill : on ne lit jamais que les phrases relues.
        builder.HasIndex(x => new { x.ScenarioId, x.ReviewedAt, x.Difficulty });
    }
}
