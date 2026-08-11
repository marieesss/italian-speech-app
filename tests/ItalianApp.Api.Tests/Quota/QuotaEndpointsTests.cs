using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ItalianApp.Api.Features.Quota;
using ItalianApp.Api.Tests.Infrastructure;

namespace ItalianApp.Api.Tests.Quota;

[Collection(DatabaseCollection.Name)]
public class QuotaEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Today_requires_authentication()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().GetAsync("/api/quota/today");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Today_reports_limits_before_any_call()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = await TestClient.AuthenticatedAsync(factory);

        var today = await client.GetFromJsonAsync<QuotaTodayResponse>("/api/quota/today");

        today!.Scoring.Limit.Should().Be(150);
        today.Scoring.Used.Should().Be(0);
        today.Scoring.Remaining.Should().Be(150);
        today.Tts.Limit.Should().Be(0);
        today.Tts.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_usage_is_refused_to_a_plain_user()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = await TestClient.AuthenticatedAsync(factory);

        var response = await client.GetAsync("/api/admin/usage?days=30");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_usage_is_served_to_a_listed_email()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using var factory = new IntegrationFactory(
            postgres.ConnectionString,
            new Dictionary<string, string?> { ["Identity:AdminEmails"] = $"someone@else.com, {email}" });

        var client = await TestClient.AuthenticatedAsync(factory, email);

        var response = await client.GetAsync("/api/admin/usage?days=30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<UsageDay>>();
        history.Should().NotBeNull();
    }
}
