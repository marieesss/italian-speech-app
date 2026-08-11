using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Progress;

public enum FeedbackSource
{
    /// <summary>Conseils issus de la table <c>PhoneticTips</c>, servis tels quels.</summary>
    Rules,

    /// <summary>Mêmes conseils, reformulés par Claude.</summary>
    Llm
}

/// <summary>Score d'un phonème isolé tel que rendu par Azure, conservé pour l'historique.</summary>
/// <param name="Word">Mot dans lequel le phonème apparaît.</param>
/// <param name="Phoneme">Symbole du phonème.</param>
/// <param name="Score">0 à 100.</param>
public record PhonemeScore(string Word, string Phoneme, double Score);

/// <summary>
/// Une répétition scorée. <b>L'audio n'est pas conservé</b> : seuls les nombres et le texte
/// du feedback survivent à la requête (cf. README, décision 3).
/// </summary>
public class Attempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PhraseId { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }

    public double OverallScore { get; set; }
    public double AccuracyScore { get; set; }
    public double FluencyScore { get; set; }
    public double CompletenessScore { get; set; }
    public double ProsodyScore { get; set; }

    public List<PhonemeScore> PhonemeScores { get; set; } = [];

    public required string FeedbackText { get; set; }
    public FeedbackSource FeedbackSource { get; set; }

    public User User { get; set; } = null!;
    public Phrase Phrase { get; set; } = null!;
}

public class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Attempts");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Phrase)
               .WithMany()
               .HasForeignKey(x => x.PhraseId)
               .OnDelete(DeleteBehavior.Cascade);

        // Sert la courbe de progression et l'historique par phrase.
        builder.HasIndex(x => new { x.UserId, x.AttemptedAt });
        builder.HasIndex(x => new { x.UserId, x.PhraseId, x.AttemptedAt });

        builder.Property(x => x.FeedbackText).HasMaxLength(4096);
        builder.Property(x => x.FeedbackSource).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.PhonemeScores).HasJsonbConversion();
    }
}
