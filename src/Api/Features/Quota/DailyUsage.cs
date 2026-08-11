using ItalianApp.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Quota;

// Resets implicitly at midnight: a new date means a new row.
public class DailyUsage
{
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }

    public int ScoringCalls { get; set; }
    public int LlmCalls { get; set; }

    // Stays at 0: runtime speech synthesis is forbidden by design.
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

        builder.HasIndex(x => x.Date);
    }
}
