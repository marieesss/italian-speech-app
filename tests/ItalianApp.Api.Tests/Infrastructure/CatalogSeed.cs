using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ItalianApp.Api.Tests.Infrastructure;

public record SeededCatalog(Category Category, Scenario Scenario);

public static class CatalogSeed
{
    // The container is shared across the collection, so every seeded category carries a
    // unique slug and tests only ever assert on their own rows.
    public static async Task<SeededCatalog> InsertAsync(
        IntegrationFactory factory,
        int reviewedPhrases = 2,
        int unreviewedPhrases = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Slug = $"cat-{Guid.NewGuid():N}",
            NameFr = "Restaurant",
            NameIt = "Ristorante",
            IconKey = "utensils",
            DisplayOrder = 10
        };

        var scenario = new Scenario
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            TitleFr = "Commander au comptoir",
            TitleIt = "Ordinare al banco",
            DescriptionFr = "Vous commandez un café debout au comptoir.",
            DisplayOrder = 10
        };

        db.Categories.Add(category);
        db.Scenarios.Add(scenario);

        for (var index = 0; index < reviewedPhrases; index++)
        {
            db.Phrases.Add(NewPhrase(scenario.Id, index, reviewed: true));
        }

        for (var index = 0; index < unreviewedPhrases; index++)
        {
            db.Phrases.Add(NewPhrase(scenario.Id, 1000 + index, reviewed: false));
        }

        await db.SaveChangesAsync();

        return new SeededCatalog(category, scenario);
    }

    private static Phrase NewPhrase(Guid scenarioId, int index, bool reviewed) => new()
    {
        Id = Guid.NewGuid(),
        ScenarioId = scenarioId,
        TextIt = $"Un caffè per favore {index}",
        TextFr = $"Un café s'il vous plaît {index}",
        ContextFr = "Au comptoir.",
        Difficulty = (index % 3) + 1,
        PhoneticTraps = ["double_consonant:ff", "final_vowel"],
        AudioUrl = $"/audio/it/{Guid.NewGuid()}.mp3",
        TtsVoice = "it-IT-ElsaNeural",
        ReviewedAt = reviewed ? DateTimeOffset.UtcNow : null
    };
}
