using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Catalog;

/// <summary>Situation concrète à l'intérieur d'une catégorie : « commander au comptoir », « demander l'addition ».</summary>
public class Scenario
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }

    public required string TitleFr { get; set; }
    public required string TitleIt { get; set; }
    public required string DescriptionFr { get; set; }
    public int DisplayOrder { get; set; }

    public Category Category { get; set; } = null!;
    public List<Phrase> Phrases { get; } = [];
}

public class ScenarioConfiguration : IEntityTypeConfiguration<Scenario>
{
    public void Configure(EntityTypeBuilder<Scenario> builder)
    {
        builder.ToTable("Scenarios");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Category)
               .WithMany(x => x.Scenarios)
               .HasForeignKey(x => x.CategoryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CategoryId, x.DisplayOrder });

        builder.Property(x => x.TitleFr).HasMaxLength(160);
        builder.Property(x => x.TitleIt).HasMaxLength(160);
        builder.Property(x => x.DescriptionFr).HasMaxLength(512);
    }
}
