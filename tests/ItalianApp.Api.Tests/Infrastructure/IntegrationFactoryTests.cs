using FluentAssertions;
using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItalianApp.Api.Tests.Infrastructure;

// Regression guard. Services that read configuration at registration time capture the
// repo .env instead, and the whole suite silently runs against the dev database.
[Collection(DatabaseCollection.Name)]
public class IntegrationFactoryTests(PostgresFixture postgres)
{
    [Fact]
    public void Test_host_talks_to_the_container_not_the_dev_database()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.GetConnectionString().Should().Be(postgres.ConnectionString);
    }
}
