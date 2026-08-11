using System.Net.Http.Headers;
using System.Net.Http.Json;
using ItalianApp.Api.Features.Identity;

namespace ItalianApp.Api.Tests.Infrastructure;

public static class TestClient
{
    // Every protected endpoint needs a real token, so tests register a throwaway account.
    public static async Task<HttpClient> AuthenticatedAsync(IntegrationFactory factory)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@example.com", "una-password-lunga", "Anna"));

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
                   ?? throw new InvalidOperationException("Registration returned no body.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        return client;
    }
}
