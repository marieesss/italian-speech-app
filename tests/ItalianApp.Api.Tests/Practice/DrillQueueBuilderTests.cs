using FluentAssertions;
using ItalianApp.Api.Features.Practice;

namespace ItalianApp.Api.Tests.Practice;

public class DrillQueueBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Weak_phrases_come_first_then_due_then_fresh()
    {
        var queue = Build(
            Candidate("fresh", difficulty: 1),
            Candidate("due", difficulty: 1, lastScore: 85, nextReview: Now.AddDays(-1)),
            Candidate("weak", difficulty: 1, lastScore: 42, nextReview: Now));

        queue.Select(item => item.Candidate.TextIt).Should().Equal("weak", "due", "fresh");
    }

    [Fact]
    public void A_phrase_not_yet_due_lands_last()
    {
        var queue = Build(
            Candidate("later", difficulty: 1, lastScore: 90, nextReview: Now.AddDays(6)),
            Candidate("fresh", difficulty: 3));

        queue.Select(item => item.Bucket).Should().Equal(DrillBucket.Fresh, DrillBucket.Later);
    }

    [Fact]
    public void Exactly_at_the_pass_mark_is_not_weak()
    {
        var queue = Build(Candidate("borderline", difficulty: 1, lastScore: 70, nextReview: Now.AddDays(7)));

        queue.Single().Bucket.Should().Be(DrillBucket.Later);
    }

    [Fact]
    public void Just_below_the_pass_mark_is_weak()
    {
        var queue = Build(Candidate("borderline", difficulty: 1, lastScore: 69.9, nextReview: Now.AddDays(7)));

        queue.Single().Bucket.Should().Be(DrillBucket.Weak);
    }

    [Fact]
    public void Difficulty_orders_within_a_bucket()
    {
        var queue = Build(
            Candidate("hard", difficulty: 3),
            Candidate("easy", difficulty: 1),
            Candidate("medium", difficulty: 2));

        queue.Select(item => item.Candidate.TextIt).Should().Equal("easy", "medium", "hard");
    }

    [Fact]
    public void Bucket_beats_difficulty()
    {
        var queue = Build(
            Candidate("easy fresh", difficulty: 1),
            Candidate("hard weak", difficulty: 3, lastScore: 30, nextReview: Now));

        queue[0].Candidate.TextIt.Should().Be("hard weak");
    }

    [Fact]
    public void Ordering_is_stable_for_equal_difficulty()
    {
        var queue = Build(
            Candidate("banana", difficulty: 1),
            Candidate("apple", difficulty: 1));

        queue.Select(item => item.Candidate.TextIt).Should().Equal("apple", "banana");
    }

    [Fact]
    public void Due_date_in_the_past_is_due()
    {
        var queue = Build(Candidate("due", difficulty: 1, lastScore: 88, nextReview: Now.AddSeconds(-1)));

        queue.Single().Bucket.Should().Be(DrillBucket.Due);
    }

    [Fact]
    public void Exactly_due_now_counts_as_due()
    {
        var queue = Build(Candidate("due", difficulty: 1, lastScore: 88, nextReview: Now));

        queue.Single().Bucket.Should().Be(DrillBucket.Due);
    }

    [Fact]
    public void Session_size_truncates_the_queue()
    {
        var candidates = Enumerable.Range(0, 20)
            .Select(index => Candidate($"phrase {index:00}", difficulty: 1))
            .ToArray();

        Build(size: 5, candidates: candidates).Should().HaveCount(5);
    }

    [Fact]
    public void An_empty_scenario_yields_an_empty_queue()
    {
        Build().Should().BeEmpty();
    }

    private static IReadOnlyList<QueuedPhrase> Build(params DrillCandidate[] candidates) =>
        DrillQueueBuilder.Build(candidates, Now, passingScore: 70, size: 10);

    private static IReadOnlyList<QueuedPhrase> Build(int size, params DrillCandidate[] candidates) =>
        DrillQueueBuilder.Build(candidates, Now, passingScore: 70, size);

    private static DrillCandidate Candidate(
        string textIt,
        int difficulty,
        double? lastScore = null,
        DateTimeOffset? nextReview = null) =>
        new(Guid.NewGuid(), difficulty, textIt, lastScore, nextReview);
}
