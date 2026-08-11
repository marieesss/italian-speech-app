using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Catalog;

public class Category
{
    public Guid Id { get; set; }

    // Stable handle used by the content pipeline.
    public required string Slug { get; set; }

    public required string NameFr { get; set; }
    public required string NameIt { get; set; }
    public string? IconKey { get; set; }
    public int DisplayOrder { get; set; }

    public List<Scenario> Scenarios { get; } = [];
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.DisplayOrder);

        builder.Property(x => x.Slug).HasMaxLength(64);
        builder.Property(x => x.NameFr).HasMaxLength(128);
        builder.Property(x => x.NameIt).HasMaxLength(128);
        builder.Property(x => x.IconKey).HasMaxLength(64);
    }
}
