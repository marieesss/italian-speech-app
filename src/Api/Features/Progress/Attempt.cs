using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Infrastructure.Persistence;
using ItalianApp.Api.Infrastructure.Speech;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Progress;

public enum FeedbackSource
{
    Rules,
    Llm
}

// One scored repetition. The audio is not stored — only the numbers survive the request.
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

        builder.HasIndex(x => new { x.UserId, x.AttemptedAt });
        builder.HasIndex(x => new { x.UserId, x.PhraseId, x.AttemptedAt });

        builder.Property(x => x.FeedbackText).HasMaxLength(4096);
        builder.Property(x => x.FeedbackSource).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.PhonemeScores).HasJsonbConversion();
    }
}
