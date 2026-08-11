using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Progress;

/// <summary>
/// État courant d'une phrase pour une utilisatrice. Une ligne par couple (utilisatrice, phrase).
/// </summary>
public class PhraseProgress
{
    public Guid UserId { get; set; }
    public Guid PhraseId { get; set; }

    public int AttemptCount { get; set; }
    public double BestScore { get; set; }
    public double LastScore { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }

    /// <summary>
    /// Prochaine échéance. En V1, renseignée par une règle simple :
    /// score &lt; 70 → immédiat (la phrase revient dans la session courante) ;
    /// score ≥ 70 → +7 jours.
    /// </summary>
    public DateTimeOffset NextReviewAt { get; set; }

    // --- Champs SM-2, modélisés en V1 mais non exploités ---------------------------
    // Présents dès maintenant pour pouvoir brancher la répétition espacée réelle en V2
    // sans migration de schéma. Aucun code de la V1 ne les lit.

    public double EaseFactor { get; set; } = 2.5;
    public int Repetitions { get; set; }
    public int IntervalDays { get; set; }

    // -------------------------------------------------------------------------------

    public User User { get; set; } = null!;
    public Phrase Phrase { get; set; } = null!;
}

public class PhraseProgressConfiguration : IEntityTypeConfiguration<PhraseProgress>
{
    public void Configure(EntityTypeBuilder<PhraseProgress> builder)
    {
        builder.ToTable("PhraseProgress");
        builder.HasKey(x => new { x.UserId, x.PhraseId });

        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Phrase)
               .WithMany()
               .HasForeignKey(x => x.PhraseId)
               .OnDelete(DeleteBehavior.Cascade);

        // Sert la constitution de la file : phrases faibles d'abord, puis échues.
        builder.HasIndex(x => new { x.UserId, x.LastScore });
        builder.HasIndex(x => new { x.UserId, x.NextReviewAt });
    }
}
