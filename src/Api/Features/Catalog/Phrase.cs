using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Catalog;

public class Phrase
{
    // Stable: also keys the audio file (/audio/it/{id}.mp3) and the user's progress.
    public Guid Id { get; set; }

    public Guid ScenarioId { get; set; }

    public required string TextIt { get; set; }
    public required string TextFr { get; set; }

    // Situation shown before the phrase itself.
    public required string ContextFr { get; set; }

    public int Difficulty { get; set; } = 1;

    // Codes, optionally parameterised: ["double_consonant:tt", "stress:prenotàre", "gli"].
    public List<string> PhoneticTraps { get; set; } = [];

    // Both set by seed-audio. Null until the MP3 exists.
    public string? AudioUrl { get; set; }
    public string? TtsVoice { get; set; }

    // A phrase without ReviewedAt is never served. Review is not optional.
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

        // Duplicate phrase within a scenario is a content error.
        builder.HasIndex(x => new { x.ScenarioId, x.TextIt }).IsUnique();

        builder.HasIndex(x => new { x.ScenarioId, x.ReviewedAt, x.Difficulty });
    }
}
