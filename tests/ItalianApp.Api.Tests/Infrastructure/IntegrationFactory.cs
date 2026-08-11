using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ItalianApp.Api.Tests.Infrastructure;

public class IntegrationFactory(string connectionString, Dictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Jwt:SigningSecret"] = "integration-tests-signing-secret-0123456789",
            ["Jwt:Issuer"] = "italian-app-tests",
            ["Jwt:Audience"] = "italian-app-tests",
            ["Identity:AllowRegistration"] = "true"
        };

        foreach (var (key, value) in overrides ?? [])
        {
            settings[key] = value;
        }

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }
}
