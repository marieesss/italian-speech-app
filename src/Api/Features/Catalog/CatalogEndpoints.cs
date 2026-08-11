using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItalianApp.Api.Features.Catalog;

public record CategoryResponse(
    Guid Id,
    string Slug,
    string NameFr,
    string NameIt,
    string? IconKey,
    int DisplayOrder,
    int ScenarioCount,
    int PhraseCount);

public record ScenarioResponse(
    Guid Id,
    string TitleFr,
    string TitleIt,
    string DescriptionFr,
    int DisplayOrder,
    int PhraseCount);

public record CategoryDetailResponse(
    Guid Id,
    string Slug,
    string NameFr,
    string NameIt,
    string? IconKey,
    IReadOnlyList<ScenarioResponse> Scenarios);

public record PhraseResponse(
    Guid Id,
    string TextIt,
    string TextFr,
    string ContextFr,
    int Difficulty,
    IReadOnlyList<string> PhoneticTraps,
    string? AudioUrl);

public record ScenarioDetailResponse(
    Guid Id,
    Guid CategoryId,
    string TitleFr,
    string TitleIt,
    string DescriptionFr,
    IReadOnlyList<PhraseResponse> Phrases);

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/catalog")
            .WithTags("Catalog")
            .RequireAuthorization();

        group.MapGet("/categories", GetCategoriesAsync);
        group.MapGet("/categories/{slug}", GetCategoryAsync);
        group.MapGet("/scenarios/{id:guid}", GetScenarioAsync);

        return routes;
    }

    private static async Task<IResult> GetCategoriesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var phraseCounts = await ReviewedPhraseCountsByCategoryAsync(db, cancellationToken);

        var categories = await db.Categories
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.NameFr)
            .Select(category => new
            {
                category.Id,
                category.Slug,
                category.NameFr,
                category.NameIt,
                category.IconKey,
                category.DisplayOrder,
                ScenarioCount = category.Scenarios.Count
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(categories
            .Select(category => new CategoryResponse(
                category.Id,
                category.Slug,
                category.NameFr,
                category.NameIt,
                category.IconKey,
                category.DisplayOrder,
                category.ScenarioCount,
                phraseCounts.GetValueOrDefault(category.Id)))
            .ToList());
    }

    private static async Task<IResult> GetCategoryAsync(
        string slug,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .SingleOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var phraseCounts = await db.Phrases
            .Reviewed()
            .Where(phrase => phrase.Scenario.CategoryId == category.Id)
            .GroupBy(phrase => phrase.ScenarioId)
            .Select(group => new { ScenarioId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.ScenarioId, entry => entry.Count, cancellationToken);

        var scenarios = await db.Scenarios
            .Where(scenario => scenario.CategoryId == category.Id)
            .OrderBy(scenario => scenario.DisplayOrder)
            .ThenBy(scenario => scenario.TitleFr)
            .Select(scenario => new
            {
                scenario.Id,
                scenario.TitleFr,
                scenario.TitleIt,
                scenario.DescriptionFr,
                scenario.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new CategoryDetailResponse(
            category.Id,
            category.Slug,
            category.NameFr,
            category.NameIt,
            category.IconKey,
            scenarios
                .Select(scenario => new ScenarioResponse(
                    scenario.Id,
                    scenario.TitleFr,
                    scenario.TitleIt,
                    scenario.DescriptionFr,
                    scenario.DisplayOrder,
                    phraseCounts.GetValueOrDefault(scenario.Id)))
                .ToList()));
    }

    private static async Task<IResult> GetScenarioAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var scenario = await db.Scenarios
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.CategoryId,
                candidate.TitleFr,
                candidate.TitleIt,
                candidate.DescriptionFr
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (scenario is null)
        {
            return Results.NotFound();
        }

        var phrases = await db.Phrases
            .Reviewed()
            .Where(phrase => phrase.ScenarioId == id)
            .OrderBy(phrase => phrase.Difficulty)
            .ThenBy(phrase => phrase.TextIt)
            .Select(phrase => new PhraseResponse(
                phrase.Id,
                phrase.TextIt,
                phrase.TextFr,
                phrase.ContextFr,
                phrase.Difficulty,
                phrase.PhoneticTraps,
                phrase.AudioUrl))
            .ToListAsync(cancellationToken);

        return Results.Ok(new ScenarioDetailResponse(
            scenario.Id,
            scenario.CategoryId,
            scenario.TitleFr,
            scenario.TitleIt,
            scenario.DescriptionFr,
            phrases));
    }

    private static Task<Dictionary<Guid, int>> ReviewedPhraseCountsByCategoryAsync(
        AppDbContext db,
        CancellationToken cancellationToken) =>
        db.Phrases
            .Reviewed()
            .GroupBy(phrase => phrase.Scenario.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.CategoryId, entry => entry.Count, cancellationToken);
}
