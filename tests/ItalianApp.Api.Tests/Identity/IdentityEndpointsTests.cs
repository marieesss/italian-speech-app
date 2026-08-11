using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Tests.Infrastructure;

namespace ItalianApp.Api.Tests.Identity;

[Collection(DatabaseCollection.Name)]
public class IdentityEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Register_returns_a_usable_token()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewAccount());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.Token.Should().NotBeEmpty();
        auth.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        me!.Id.Should().Be(auth.User.Id);
    }

    [Fact]
    public async Task Email_is_stored_lowercase_and_trimmed()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        var account = NewAccount();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            account with { Email = $"  {account.Email.ToUpperInvariant()} " });

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.User.Email.Should().Be(account.Email);
    }

    [Fact]
    public async Task Registering_a_known_email_conflicts()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        var account = NewAccount();

        await client.PostAsJsonAsync("/api/auth/register", account);
        var second = await client.PostAsJsonAsync("/api/auth/register", account);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("not-an-email", "longenoughpassword", "Anna")]
    [InlineData("valid@example.com", "short", "Anna")]
    [InlineData("valid@example.com", "longenoughpassword", "  ")]
    public async Task Invalid_input_is_rejected(string email, string password, string displayName)
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, password, displayName));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registration_can_be_closed()
    {
        using var factory = new IntegrationFactory(
            postgres.ConnectionString,
            new Dictionary<string, string?> { ["Identity:AllowRegistration"] = "false" });

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewAccount());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        var account = NewAccount();
        await client.PostAsJsonAsync("/api/auth/register", account);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, account.Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_answer_alike()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = factory.CreateClient();
        var account = NewAccount();
        await client.PostAsJsonAsync("/api/auth/register", account);

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, "totally-wrong-password"));

        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest($"ghost-{Guid.NewGuid():N}@example.com", account.Password));

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var first = await wrongPassword.Content.ReadAsStringAsync();
        var second = await unknownEmail.Content.ReadAsStringAsync();
        first.Should().Be(second);
    }

    [Fact]
    public async Task Me_requires_a_token()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static RegisterRequest NewAccount() =>
        new($"anna-{Guid.NewGuid():N}@example.com", "una-password-lunga", "Anna");
}
