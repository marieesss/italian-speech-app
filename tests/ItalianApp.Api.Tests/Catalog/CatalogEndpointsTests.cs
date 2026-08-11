using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Tests.Infrastructure;

namespace ItalianApp.Api.Tests.Catalog;

[Collection(DatabaseCollection.Name)]
public class CatalogEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Catalog_requires_authentication()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().GetAsync("/api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Categories_report_their_scenario_and_phrase_counts()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 3);
        var client = await TestClient.AuthenticatedAsync(factory);

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/catalog/categories");

        var category = categories!.Single(candidate => candidate.Id == seeded.Category.Id);
        category.Slug.Should().Be(seeded.Category.Slug);
        category.ScenarioCount.Should().Be(1);
        category.PhraseCount.Should().Be(3);
    }

    [Fact]
    public async Task Unreviewed_phrases_are_not_counted()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 2, unreviewedPhrases: 5);
        var client = await TestClient.AuthenticatedAsync(factory);

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/catalog/categories");

        categories!.Single(candidate => candidate.Id == seeded.Category.Id)
            .PhraseCount.Should().Be(2);
    }

    [Fact]
    public async Task Category_detail_lists_its_scenarios()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 4);
        var client = await TestClient.AuthenticatedAsync(factory);

        var category = await client.GetFromJsonAsync<CategoryDetailResponse>(
            $"/api/catalog/categories/{seeded.Category.Slug}");

        var scenario = category!.Scenarios.Should().ContainSingle().Subject;
        scenario.Id.Should().Be(seeded.Scenario.Id);
        scenario.TitleIt.Should().Be("Ordinare al banco");
        scenario.PhraseCount.Should().Be(4);
    }

    [Fact]
    public async Task Unknown_category_slug_is_not_found()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = await TestClient.AuthenticatedAsync(factory);

        var response = await client.GetAsync("/api/catalog/categories/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Scenario_detail_serves_reviewed_phrases_only()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 2, unreviewedPhrases: 3);
        var client = await TestClient.AuthenticatedAsync(factory);

        var scenario = await client.GetFromJsonAsync<ScenarioDetailResponse>(
            $"/api/catalog/scenarios/{seeded.Scenario.Id}");

        scenario!.Phrases.Should().HaveCount(2);
        scenario.CategoryId.Should().Be(seeded.Category.Id);
    }

    [Fact]
    public async Task Phrases_come_back_by_ascending_difficulty()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 6);
        var client = await TestClient.AuthenticatedAsync(factory);

        var scenario = await client.GetFromJsonAsync<ScenarioDetailResponse>(
            $"/api/catalog/scenarios/{seeded.Scenario.Id}");

        scenario!.Phrases.Select(phrase => phrase.Difficulty).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Phonetic_traps_survive_the_jsonb_round_trip()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);

        var scenario = await client.GetFromJsonAsync<ScenarioDetailResponse>(
            $"/api/catalog/scenarios/{seeded.Scenario.Id}");

        scenario!.Phrases.Single().PhoneticTraps
            .Should().Equal("double_consonant:ff", "final_vowel");
    }

    [Fact]
    public async Task Unknown_scenario_is_not_found()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = await TestClient.AuthenticatedAsync(factory);

        var response = await client.GetAsync($"/api/catalog/scenarios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
