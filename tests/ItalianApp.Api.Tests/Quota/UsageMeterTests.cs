using FluentAssertions;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Features.Quota;
using ItalianApp.Api.Infrastructure.Persistence;
using ItalianApp.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ItalianApp.Api.Tests.Quota;

[Collection(DatabaseCollection.Name)]
public class UsageMeterTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Consumption_stops_at_the_limit()
    {
        using var factory = Factory(scoringLimit: 3);
        var userId = await NewUserAsync(factory);

        var decisions = new List<QuotaDecision>();
        for (var call = 0; call < 5; call++)
        {
            decisions.Add(await ConsumeAsync(factory, userId, QuotaKind.Scoring));
        }

        decisions.Count(decision => decision.Allowed).Should().Be(3);
        decisions[^1].Used.Should().Be(3);
        decisions[^1].Remaining.Should().Be(0);
    }

    [Fact]
    public async Task Counters_are_independent()
    {
        using var factory = Factory(scoringLimit: 1, llmLimit: 5);
        var userId = await NewUserAsync(factory);

        await ConsumeAsync(factory, userId, QuotaKind.Scoring);
        await ConsumeAsync(factory, userId, QuotaKind.Scoring);
        var llm = await ConsumeAsync(factory, userId, QuotaKind.Llm);

        llm.Allowed.Should().BeTrue();
        llm.Used.Should().Be(1);
    }

    [Fact]
    public async Task Users_do_not_share_a_counter()
    {
        using var factory = Factory(scoringLimit: 1);
        var first = await NewUserAsync(factory);
        var second = await NewUserAsync(factory);

        await ConsumeAsync(factory, first, QuotaKind.Scoring);
        var other = await ConsumeAsync(factory, second, QuotaKind.Scoring);

        other.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Runtime_tts_is_always_refused_and_writes_nothing()
    {
        using var factory = Factory();
        var userId = await NewUserAsync(factory);

        var decision = await ConsumeAsync(factory, userId, QuotaKind.Tts);

        decision.Allowed.Should().BeFalse();
        decision.Limit.Should().Be(0);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DailyUsages.Any(row => row.UserId == userId).Should().BeFalse();
    }

    [Fact]
    public async Task Counters_reset_after_local_midnight()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 11, 21, 30, 0, TimeSpan.Zero));
        using var factory = Factory(scoringLimit: 1, clock: clock);
        var userId = await NewUserAsync(factory);

        await ConsumeAsync(factory, userId, QuotaKind.Scoring);
        var blocked = await ConsumeAsync(factory, userId, QuotaKind.Scoring);

        // 21:30 UTC is 23:30 in Paris; one more hour crosses the local day boundary.
        clock.Advance(TimeSpan.FromHours(1));
        var afterMidnight = await ConsumeAsync(factory, userId, QuotaKind.Scoring);

        blocked.Allowed.Should().BeFalse();
        afterMidnight.Allowed.Should().BeTrue();
        afterMidnight.Used.Should().Be(1);
    }

    [Fact]
    public async Task Utc_midnight_alone_does_not_reset_the_day()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 11, 23, 30, 0, TimeSpan.Zero));
        using var factory = Factory(scoringLimit: 1, clock: clock);
        var userId = await NewUserAsync(factory);

        await ConsumeAsync(factory, userId, QuotaKind.Scoring);

        // Crosses UTC midnight, but Paris is already on the 12th and stays there.
        clock.Advance(TimeSpan.FromHours(1));
        var decision = await ConsumeAsync(factory, userId, QuotaKind.Scoring);

        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Parallel_calls_never_exceed_the_limit()
    {
        using var factory = Factory(scoringLimit: 5);
        var userId = await NewUserAsync(factory);
        var scopes = factory.Services.GetRequiredService<IServiceScopeFactory>();

        var decisions = await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
        {
            using var scope = scopes.CreateScope();
            var meter = scope.ServiceProvider.GetRequiredService<IUsageMeter>();
            return await meter.TryConsumeAsync(userId, QuotaKind.Scoring);
        }));

        decisions.Count(decision => decision.Allowed).Should().Be(5);
    }

    [Fact]
    public async Task Unknown_time_zone_falls_back_to_utc()
    {
        using var factory = Factory(overrides: new Dictionary<string, string?>
        {
            ["Quota:TimeZone"] = "Mars/Olympus_Mons"
        });
        var userId = await NewUserAsync(factory);

        var decision = await ConsumeAsync(factory, userId, QuotaKind.Scoring);

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task History_aggregates_by_day()
    {
        using var factory = Factory(scoringLimit: 10);
        var first = await NewUserAsync(factory);
        var second = await NewUserAsync(factory);

        await ConsumeAsync(factory, first, QuotaKind.Scoring);
        await ConsumeAsync(factory, first, QuotaKind.Scoring);
        await ConsumeAsync(factory, second, QuotaKind.Scoring);
        await ConsumeAsync(factory, first, QuotaKind.Llm);

        using var scope = factory.Services.CreateScope();
        var meter = scope.ServiceProvider.GetRequiredService<IUsageMeter>();
        var history = await meter.GetHistoryAsync(30);

        // Not history[0]: the clock-shifting tests in this class leave future-dated rows.
        var today = history.Single(day => day.Date == ((UsageMeter)meter).Today());
        today.ScoringCalls.Should().BeGreaterThanOrEqualTo(3);
        today.LlmCalls.Should().BeGreaterThanOrEqualTo(1);
    }

    private IntegrationFactory Factory(
        int scoringLimit = 150,
        int llmLimit = 100,
        TimeProvider? clock = null,
        Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Quota:ScoringCallsPerDay"] = scoringLimit.ToString(),
            ["Quota:LlmCallsPerDay"] = llmLimit.ToString()
        };

        foreach (var (key, value) in overrides ?? [])
        {
            settings[key] = value;
        }

        return new IntegrationFactory(
            postgres.ConnectionString,
            settings,
            clock is null ? null : services => services.AddSingleton(clock));
    }

    private static async Task<Guid> NewUserAsync(IntegrationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"quota-{Guid.NewGuid():N}@example.com",
            DisplayName = "Anna",
            PasswordHash = "irrelevant",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private static async Task<QuotaDecision> ConsumeAsync(IntegrationFactory factory, Guid userId, QuotaKind kind)
    {
        using var scope = factory.Services.CreateScope();
        var meter = scope.ServiceProvider.GetRequiredService<IUsageMeter>();

        return await meter.TryConsumeAsync(userId, kind);
    }
}
