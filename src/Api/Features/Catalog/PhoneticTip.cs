using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Catalog;

// Content, not code: edited in the database, no redeploy. Lets a weak phoneme score
// resolve to advice without calling the LLM.
public class PhoneticTip
{
    // Code without argument: double_consonant, gli, rolled_r.
    public required string Code { get; set; }

    public required string LabelFr { get; set; }
    public required string AdviceFr { get; set; }

    // Azure phoneme symbols that trigger this tip when scored low.
    // Empty means the tip only fires from a phrase's phoneticTraps annotation.
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
