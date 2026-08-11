using Microsoft.EntityFrameworkCore;

namespace ItalianApp.Api.Infrastructure.Persistence;

public static class DatabaseSetup
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing connection string 'Default'.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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
