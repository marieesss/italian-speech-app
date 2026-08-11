using System.Security.Claims;
using ItalianApp.Api.Features.Identity;
using Microsoft.Extensions.Options;

namespace ItalianApp.Api.Features.Quota;

public record QuotaCounterResponse(int Used, int Limit, int Remaining, bool Allowed);

public record QuotaTodayResponse(
    DateOnly Date,
    QuotaCounterResponse Scoring,
    QuotaCounterResponse Llm,
    QuotaCounterResponse Tts);

public static class QuotaEndpoints
{
    private const int MaxHistoryDays = 90;

    public static IEndpointRouteBuilder MapQuotaEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/quota/today", GetTodayAsync)
            .WithTags("Quota")
            .RequireAuthorization();

        routes.MapGet("/api/admin/usage", GetUsageAsync)
            .WithTags("Quota")
            .RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> GetTodayAsync(
        ClaimsPrincipal principal,
        IUsageMeter meter,
        CancellationToken cancellationToken)
    {
        var status = await meter.GetTodayAsync(principal.Id(), cancellationToken);

        return Results.Ok(new QuotaTodayResponse(
            status.Date,
            Counter(status.Scoring),
            Counter(status.Llm),
            Counter(status.Tts)));
    }

    private static async Task<IResult> GetUsageAsync(
        ClaimsPrincipal principal,
        IUsageMeter meter,
        IOptions<AccountOptions> accountOptions,
        int days,
        CancellationToken cancellationToken)
    {
        if (!accountOptions.Value.IsAdmin(principal.Email()))
        {
            return Results.Problem(
                title: "Administration endpoint",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var window = days is <= 0 or > MaxHistoryDays ? 30 : days;

        return Results.Ok(await meter.GetHistoryAsync(window, cancellationToken));
    }

    private static QuotaCounterResponse Counter(QuotaDecision decision) =>
        new(decision.Used, decision.Limit, decision.Remaining, decision.Allowed);
}
