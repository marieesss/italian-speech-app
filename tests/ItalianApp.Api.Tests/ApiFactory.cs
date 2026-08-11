using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ItalianApp.Api.Tests;

/// <summary>
/// Hôte de test pour les endpoints qui ne touchent pas la base.
/// Coupe la migration au démarrage : sans cela, le simple fait de créer un client
/// exigerait un PostgreSQL joignable.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused;Password=unused"
            });
        });
    }
}
