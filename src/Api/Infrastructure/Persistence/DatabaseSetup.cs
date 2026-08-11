using Microsoft.EntityFrameworkCore;

namespace ItalianApp.Api.Infrastructure.Persistence;

public static class DatabaseSetup
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Resolved from the provider, not captured at registration: configuration sources
        // added later (WebApplicationFactory in tests) must still win.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();

            options.UseNpgsql(configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Missing connection string 'Default'."));
        });

        return services;
    }

    // Turn off Database:AutoMigrate when the deployment migrates in a separate step.
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
