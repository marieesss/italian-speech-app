using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItalianApp.Api.Features.Quota;

public record QuotaDecision(bool Allowed, int Used, int Limit)
{
    public int Remaining => Math.Max(0, Limit - Used);
}

public record QuotaStatus(DateOnly Date, QuotaDecision Scoring, QuotaDecision Llm, QuotaDecision Tts);

public record UsageDay(DateOnly Date, int ScoringCalls, int LlmCalls, int TtsCalls);

public interface IUsageMeter
{
    Task<QuotaDecision> TryConsumeAsync(Guid userId, QuotaKind kind, CancellationToken cancellationToken = default);

    Task<QuotaStatus> GetTodayAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageDay>> GetHistoryAsync(int days, CancellationToken cancellationToken = default);
}

public class UsageMeter(AppDbContext db, IOptions<QuotaOptions> options, TimeProvider clock, ILogger<UsageMeter> logger)
    : IUsageMeter
{
    private readonly QuotaOptions _options = options.Value;
    private TimeZoneInfo? _zone;

    public async Task<QuotaDecision> TryConsumeAsync(
        Guid userId,
        QuotaKind kind,
        CancellationToken cancellationToken = default)
    {
        var limit = _options.LimitFor(kind);
        var today = Today();

        if (limit <= 0)
        {
            return new QuotaDecision(Allowed: false, Used: await ReadAsync(userId, kind, today, cancellationToken), limit);
        }

        var column = ColumnOf(kind);

        // Read-then-write would let two concurrent attempts both pass the last slot.
        // The conditional upsert makes the check and the increment one statement.
        var sql = $$"""
                    INSERT INTO "DailyUsage" ("UserId", "Date", "ScoringCalls", "LlmCalls", "TtsCalls")
                    VALUES ({0}, {1}, {{ValuesFor(kind)}})
                    ON CONFLICT ("UserId", "Date") DO UPDATE
                       SET "{{column}}" = "DailyUsage"."{{column}}" + 1
                     WHERE "DailyUsage"."{{column}}" < {2}
                    RETURNING "{{column}}" AS "Value"
                    """;

        var updated = await db.Database
            .SqlQueryRaw<int>(sql, userId, today, limit)
            .ToListAsync(cancellationToken);

        if (updated.Count > 0)
        {
            return new QuotaDecision(Allowed: true, updated[0], limit);
        }

        return new QuotaDecision(Allowed: false, await ReadAsync(userId, kind, today, cancellationToken), limit);
    }

    public async Task<QuotaStatus> GetTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = Today();

        var usage = await db.DailyUsages
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId && row.Date == today, cancellationToken);

        return new QuotaStatus(
            today,
            Decision(usage?.ScoringCalls ?? 0, _options.ScoringCallsPerDay),
            Decision(usage?.LlmCalls ?? 0, _options.LlmCallsPerDay),
            Decision(usage?.TtsCalls ?? 0, _options.TtsCallsPerDay));
    }

    public async Task<IReadOnlyList<UsageDay>> GetHistoryAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        var from = Today().AddDays(-Math.Max(1, days) + 1);

        // Projected to an anonymous type first: EF cannot build a record through its
        // constructor from grouped aggregates.
        var rows = await db.DailyUsages
            .AsNoTracking()
            .Where(row => row.Date >= from)
            .GroupBy(row => row.Date)
            .Select(group => new
            {
                Date = group.Key,
                ScoringCalls = group.Sum(row => row.ScoringCalls),
                LlmCalls = group.Sum(row => row.LlmCalls),
                TtsCalls = group.Sum(row => row.TtsCalls)
            })
            .OrderByDescending(row => row.Date)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new UsageDay(row.Date, row.ScoringCalls, row.LlmCalls, row.TtsCalls))
            .ToList();
    }

    public DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone()).DateTime);

    private TimeZoneInfo Zone()
    {
        if (_zone is not null)
        {
            return _zone;
        }

        // Windows resolves "Romance Standard Time", Linux resolves "Europe/Paris", and
        // neither accepts the other's spelling. Config stays IANA; the id is translated
        // when the direct lookup fails.
        if (TryFind(_options.TimeZone, out var zone)
            || (TimeZoneInfo.TryConvertIanaIdToWindowsId(_options.TimeZone, out var windowsId)
                && TryFind(windowsId, out zone))
            || (TimeZoneInfo.TryConvertWindowsIdToIanaId(_options.TimeZone, out var ianaId)
                && TryFind(ianaId, out zone)))
        {
            return _zone = zone!;
        }

        logger.LogWarning("Unknown time zone {TimeZone}, counting days in UTC.", _options.TimeZone);

        return _zone = TimeZoneInfo.Utc;
    }

    private static bool TryFind(string? id, out TimeZoneInfo? zone)
    {
        try
        {
            zone = id is null ? null : TimeZoneInfo.FindSystemTimeZoneById(id);
            return zone is not null;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = null;
            return false;
        }
    }

    private async Task<int> ReadAsync(Guid userId, QuotaKind kind, DateOnly date, CancellationToken cancellationToken)
    {
        var usage = await db.DailyUsages
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId && row.Date == date, cancellationToken);

        if (usage is null)
        {
            return 0;
        }

        return kind switch
        {
            QuotaKind.Scoring => usage.ScoringCalls,
            QuotaKind.Llm => usage.LlmCalls,
            QuotaKind.Tts => usage.TtsCalls,
            _ => 0
        };
    }

    private static QuotaDecision Decision(int used, int limit) => new(used < limit, used, limit);

    private static string ColumnOf(QuotaKind kind) => kind switch
    {
        QuotaKind.Scoring => "ScoringCalls",
        QuotaKind.Llm => "LlmCalls",
        QuotaKind.Tts => "TtsCalls",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string ValuesFor(QuotaKind kind) => kind switch
    {
        QuotaKind.Scoring => "1, 0, 0",
        QuotaKind.Llm => "0, 1, 0",
        QuotaKind.Tts => "0, 0, 1",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
