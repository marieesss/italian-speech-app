using ItalianApp.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Quota;

/// <summary>
/// Compteurs d'appels externes consommés dans la journée, par utilisatrice.
/// Réinitialisation implicite à minuit : une nouvelle date crée une nouvelle ligne.
/// </summary>
public class DailyUsage
{
    public Guid UserId { get; set; }

    /// <summary>Date locale de l'application (pas d'heure), clé de partition du compteur.</summary>
    public DateOnly Date { get; set; }

    public int ScoringCalls { get; set; }
    public int LlmCalls { get; set; }

    /// <summary>Doit rester à 0 : la synthèse vocale au runtime est interdite par conception.</summary>
    public int TtsCalls { get; set; }

    public User User { get; set; } = null!;
}

public class DailyUsageConfiguration : IEntityTypeConfiguration<DailyUsage>
{
    public void Configure(EntityTypeBuilder<DailyUsage> builder)
    {
        builder.ToTable("DailyUsage");
        builder.HasKey(x => new { x.UserId, x.Date });

        builder.HasOne(x => x.User)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        // Sert l'endpoint d'observabilité : consommation des 30 derniers jours.
        builder.HasIndex(x => x.Date);
    }
}
