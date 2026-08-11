using Microsoft.EntityFrameworkCore;

namespace ItalianApp.Api.Infrastructure.Persistence;

public static class DatabaseSetup
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Chaîne de connexion 'Default' absente de la configuration.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }

    /// <summary>
    /// Applique les migrations en attente et complète la table des conseils phonétiques.
    /// Piloté par <c>Database:AutoMigrate</c> (vrai par défaut) : à couper si le déploiement
    /// applique les migrations dans une étape séparée.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:AutoMigrate", defaultValue: true))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await PhoneticTipSeeder.SeedMissingAsync(db);
    }
}
