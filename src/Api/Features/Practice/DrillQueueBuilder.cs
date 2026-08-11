namespace ItalianApp.Api.Features.Practice;

public enum DrillBucket
{
    Weak,
    Due,
    Fresh,
    Later
}

public record DrillCandidate(
    Guid PhraseId,
    int Difficulty,
    string TextIt,
    double? LastScore,
    DateTimeOffset? NextReviewAt);

public record QueuedPhrase(DrillCandidate Candidate, DrillBucket Bucket);

public static class DrillQueueBuilder
{
    public static IReadOnlyList<QueuedPhrase> Build(
        IEnumerable<DrillCandidate> candidates,
        DateTimeOffset now,
        double passingScore,
        int size) =>
        candidates
            .Select(candidate => new QueuedPhrase(candidate, BucketOf(candidate, now, passingScore)))
            .OrderBy(queued => queued.Bucket)
            .ThenBy(queued => queued.Candidate.Difficulty)
            .ThenBy(queued => queued.Candidate.TextIt, StringComparer.Ordinal)
            .Take(Math.Max(1, size))
            .ToList();

    private static DrillBucket BucketOf(DrillCandidate candidate, DateTimeOffset now, double passingScore)
    {
        if (candidate.LastScore is null)
        {
            return DrillBucket.Fresh;
        }

        if (candidate.LastScore < passingScore)
        {
            return DrillBucket.Weak;
        }

        // Later is only reached once everything else is exhausted: the learner may still
        // want to drill a scenario she has already cleared.
        return candidate.NextReviewAt is null || candidate.NextReviewAt <= now
            ? DrillBucket.Due
            : DrillBucket.Later;
    }
}
