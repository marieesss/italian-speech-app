namespace ItalianApp.Api.Features.Catalog;

public static class CatalogQueries
{
    // The one place the review gate is expressed. Every read that reaches the learner
    // goes through it; the content CLI queries db.Phrases directly instead.
    public static IQueryable<Phrase> Reviewed(this IQueryable<Phrase> phrases) =>
        phrases.Where(phrase => phrase.ReviewedAt != null);
}
