using System.Security.Claims;
using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Features.Quota;
using ItalianApp.Api.Infrastructure.Llm;
using ItalianApp.Api.Infrastructure.Persistence;
using ItalianApp.Api.Infrastructure.Speech;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItalianApp.Api.Features.Practice;

public record SessionPhraseResponse(
    Guid Id,
    string TextIt,
    string TextFr,
    string ContextFr,
    int Difficulty,
    IReadOnlyList<string> PhoneticTraps,
    string? AudioUrl,
    string Bucket,
    double? LastScore);

public record SessionResponse(Guid ScenarioId, IReadOnlyList<SessionPhraseResponse> Phrases);

public record AdviceResponse(string Code, string Label, string Advice, string? Focus);

public record WeakPhonemeResponse(string Word, string Phoneme, double Score);

public record AttemptResponse(
    Guid AttemptId,
    double OverallScore,
    double AccuracyScore,
    double FluencyScore,
    double CompletenessScore,
    double ProsodyScore,
    bool Passed,
    string FeedbackText,
    string FeedbackSource,
    IReadOnlyList<AdviceResponse> Advice,
    IReadOnlyList<WeakPhonemeResponse> WeakPhonemes,
    DateTimeOffset NextReviewAt);

public static class PracticeEndpoints
{
    public static IEndpointRouteBuilder MapPracticeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/practice")
            .WithTags("Practice")
            .RequireAuthorization();

        group.MapGet("/session", GetSessionAsync);
        group.MapPost("/phrases/{phraseId:guid}/attempts", SubmitAttemptAsync)
            .DisableAntiforgery();

        return routes;
    }

    private static async Task<IResult> GetSessionAsync(
        Guid scenarioId,
        int? size,
        ClaimsPrincipal principal,
        AppDbContext db,
        IOptions<PracticeOptions> practiceOptions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var options = practiceOptions.Value;
        var userId = principal.Id();

        if (!await db.Scenarios.AnyAsync(scenario => scenario.Id == scenarioId, cancellationToken))
        {
            return Results.NotFound();
        }

        // A phrase with no model audio cannot be drilled: the loop starts by listening.
        var phrases = await db.Phrases
            .Reviewed()
            .Where(phrase => phrase.ScenarioId == scenarioId && phrase.AudioUrl != null)
            .Select(phrase => new
            {
                phrase.Id,
                phrase.TextIt,
                phrase.TextFr,
                phrase.ContextFr,
                phrase.Difficulty,
                phrase.PhoneticTraps,
                phrase.AudioUrl
            })
            .ToListAsync(cancellationToken);

        var progress = await db.PhraseProgresses
            .Where(row => row.UserId == userId && row.Phrase.ScenarioId == scenarioId)
            .Select(row => new { row.PhraseId, row.LastScore, row.NextReviewAt })
            .ToDictionaryAsync(row => row.PhraseId, cancellationToken);

        var queue = DrillQueueBuilder.Build(
            phrases.Select(phrase => new DrillCandidate(
                phrase.Id,
                phrase.Difficulty,
                phrase.TextIt,
                progress.TryGetValue(phrase.Id, out var row) ? row.LastScore : null,
                progress.TryGetValue(phrase.Id, out var due) ? due.NextReviewAt : null)),
            clock.GetUtcNow(),
            options.PassingScore,
            size ?? options.DefaultSessionSize);

        var byId = phrases.ToDictionary(phrase => phrase.Id);

        return Results.Ok(new SessionResponse(
            scenarioId,
            queue
                .Select(queued =>
                {
                    var phrase = byId[queued.Candidate.PhraseId];
                    return new SessionPhraseResponse(
                        phrase.Id,
                        phrase.TextIt,
                        phrase.TextFr,
                        phrase.ContextFr,
                        phrase.Difficulty,
                        phrase.PhoneticTraps,
                        phrase.AudioUrl,
                        queued.Bucket.ToString(),
                        queued.Candidate.LastScore);
                })
                .ToList()));
    }

    private static async Task<IResult> SubmitAttemptAsync(
        Guid phraseId,
        HttpContext context,
        ClaimsPrincipal principal,
        AppDbContext db,
        IUsageMeter meter,
        IPronunciationScorer scorer,
        IFeedbackWriter feedbackWriter,
        RuleBasedFeedbackWriter fallbackWriter,
        AttemptRecorder recorder,
        IOptions<PracticeOptions> practiceOptions,
        CancellationToken cancellationToken)
    {
        var options = practiceOptions.Value;
        var userId = principal.Id();

        if (!AudioUpload.IsMultipart(context.Request.ContentType))
        {
            return Results.Problem(
                title: "Audio manquant",
                detail: "La tentative doit être envoyée en multipart, avec une partie « audio ».",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var phrase = await db.Phrases
            .Reviewed()
            .SingleOrDefaultAsync(candidate => candidate.Id == phraseId, cancellationToken);

        if (phrase is null)
        {
            return Results.NotFound();
        }

        var scoringQuota = await meter.TryConsumeAsync(userId, QuotaKind.Scoring, cancellationToken);

        if (!scoringQuota.Allowed)
        {
            return Results.Problem(
                title: "Quota d'entraînement atteint",
                detail: $"Vous avez utilisé vos {scoringQuota.Limit} évaluations du jour. "
                        + "Le compteur repart à minuit.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var sizeLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeLimit is { IsReadOnly: false })
        {
            sizeLimit.MaxRequestBodySize = options.MaxAudioBytes;
        }

        PronunciationAssessment assessment;

        try
        {
            assessment = await AudioUpload.ReadAsync(
                context.Request,
                audio => scorer.ScoreAsync(audio, phrase.TextIt, cancellationToken),
                cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Results.Problem(
                title: "Audio illisible",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tips = await db.PhoneticTips.AsNoTracking().ToDictionaryAsync(tip => tip.Code, cancellationToken);
        var resolved = PhoneticAdviceResolver.Resolve(phrase.PhoneticTraps, assessment, tips, options);

        // Running out of LLM quota must never interrupt practice: only the wording changes.
        var llmQuota = await meter.TryConsumeAsync(userId, QuotaKind.Llm, cancellationToken);
        var writer = llmQuota.Allowed ? feedbackWriter : fallbackWriter;

        var feedback = await writer.WriteAsync(
            new FeedbackRequest(phrase.TextIt, phrase.TextFr, assessment, resolved.Advice),
            cancellationToken);

        var attempt = await recorder.RecordAsync(
            userId,
            phraseId,
            assessment,
            feedback.Text,
            feedback.FromLlm,
            cancellationToken);

        return Results.Ok(new AttemptResponse(
            attempt.Id,
            assessment.OverallScore,
            assessment.AccuracyScore,
            assessment.FluencyScore,
            assessment.CompletenessScore,
            assessment.ProsodyScore,
            assessment.OverallScore >= options.PassingScore,
            feedback.Text,
            attempt.FeedbackSource.ToString().ToLowerInvariant(),
            resolved.Advice
                .Select(advice => new AdviceResponse(advice.Code, advice.LabelFr, advice.AdviceFr, advice.Argument))
                .ToList(),
            resolved.WeakPhonemes
                .Select(weak => new WeakPhonemeResponse(weak.Word, weak.Phoneme, weak.Score))
                .ToList(),
            await NextReviewAtAsync(db, userId, phraseId, cancellationToken)));
    }

    private static async Task<DateTimeOffset> NextReviewAtAsync(
        AppDbContext db,
        Guid userId,
        Guid phraseId,
        CancellationToken cancellationToken) =>
        await db.PhraseProgresses
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.PhraseId == phraseId)
            .Select(row => row.NextReviewAt)
            .SingleAsync(cancellationToken);
}
