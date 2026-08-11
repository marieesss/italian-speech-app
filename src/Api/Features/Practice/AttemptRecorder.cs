using ItalianApp.Api.Features.Progress;
using ItalianApp.Api.Infrastructure.Persistence;
using ItalianApp.Api.Infrastructure.Speech;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItalianApp.Api.Features.Practice;

public class AttemptRecorder(AppDbContext db, IOptions<PracticeOptions> options, TimeProvider clock)
{
    private readonly PracticeOptions _options = options.Value;

    public async Task<Attempt> RecordAsync(
        Guid userId,
        Guid phraseId,
        PronunciationAssessment assessment,
        string feedbackText,
        bool fromLlm,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var attempt = new Attempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PhraseId = phraseId,
            AttemptedAt = now,
            OverallScore = assessment.OverallScore,
            AccuracyScore = assessment.AccuracyScore,
            FluencyScore = assessment.FluencyScore,
            CompletenessScore = assessment.CompletenessScore,
            ProsodyScore = assessment.ProsodyScore,
            PhonemeScores = assessment.PhonemeScores.ToList(),
            FeedbackText = feedbackText,
            FeedbackSource = fromLlm ? FeedbackSource.Llm : FeedbackSource.Rules
        };

        db.Attempts.Add(attempt);

        await UpdateProgressAsync(userId, phraseId, assessment.OverallScore, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return attempt;
    }

    private async Task UpdateProgressAsync(
        Guid userId,
        Guid phraseId,
        double score,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var progress = await db.PhraseProgresses
            .SingleOrDefaultAsync(row => row.UserId == userId && row.PhraseId == phraseId, cancellationToken);

        if (progress is null)
        {
            progress = new PhraseProgress { UserId = userId, PhraseId = phraseId };
            db.PhraseProgresses.Add(progress);
        }

        progress.AttemptCount++;
        progress.LastScore = score;
        progress.BestScore = Math.Max(progress.BestScore, score);
        progress.LastAttemptAt = now;

        // V1 rule, deliberately not SM-2: below the pass mark the phrase returns in the
        // current session, otherwise it waits a week. The SM-2 fields stay untouched.
        progress.NextReviewAt = score < _options.PassingScore
            ? now
            : now.AddDays(_options.ReviewIntervalDays);
    }
}
