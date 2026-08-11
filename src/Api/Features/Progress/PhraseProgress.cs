using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Progress;

public class PhraseProgress
{
    public Guid UserId { get; set; }
    public Guid PhraseId { get; set; }

    public int AttemptCount { get; set; }
    public double BestScore { get; set; }
    public double LastScore { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }

    // V1 rule: score < 70 -> now (comes back in the current session), otherwise +7 days.
    public DateTimeOffset NextReviewAt { get; set; }

    // SM-2 fields, modelled but unused in V1 so V2 needs no schema migration.
    public double EaseFactor { get; set; } = 2.5;
    public int Repetitions { get; set; }
    public int IntervalDays { get; set; }

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

        // Both serve queue building: weak phrases first, then the ones that are due.
        builder.HasIndex(x => new { x.UserId, x.LastScore });
        builder.HasIndex(x => new { x.UserId, x.NextReviewAt });
    }
}
