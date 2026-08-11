using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ItalianApp.Api.Infrastructure.Persistence;

/// <summary>
/// Utilisé uniquement par <c>dotnet ef</c>. Évite que l'outillage démarre l'application
/// complète — et donc tente d'appliquer les migrations qu'il est justement en train de créer.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5434;Database=italianapp;Username=italianapp;Password=italianapp";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
