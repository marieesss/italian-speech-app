using Testcontainers.PostgreSql;

namespace ItalianApp.Api.Tests.Infrastructure;

// A throwaway PostgreSQL rather than an in-memory provider: the model relies on jsonb,
// check constraints and composite keys that the in-memory provider silently ignores.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}
