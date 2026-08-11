using System.Security.Cryptography;
using System.Text;

namespace ItalianApp.Api.Infrastructure.Speech;

// Deterministic: the same reference text always yields the same scores, so integration
// tests can assert on them without pinning magic numbers per run.
public class FakePronunciationScorer : IPronunciationScorer
{
    // Set by tests that need a specific outcome (a failing score, a missing word).
    public Func<string, PronunciationAssessment>? Script { get; set; }

    public int CallCount { get; private set; }

    public async Task<PronunciationAssessment> ScoreAsync(
        Stream audio,
        string referenceText,
        CancellationToken cancellationToken = default)
    {
        // Drain the stream the way a real scorer would, then let it go.
        await using var sink = Stream.Null;
        await audio.CopyToAsync(sink, cancellationToken);

        CallCount++;

        return Script?.Invoke(referenceText) ?? Derive(referenceText);
    }

    private static PronunciationAssessment Derive(string referenceText)
    {
        var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(referenceText)));
        var random = new Random(seed);

        var accuracy = random.Next(55, 100);
        var fluency = random.Next(55, 100);
        var completeness = random.Next(80, 101);
        var prosody = random.Next(55, 100);
        var overall = Math.Round((accuracy + fluency + completeness + prosody) / 4.0, 1);

        var phonemes = referenceText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new PhonemeScore(
                word.Trim('.', ',', '!', '?'),
                word.Trim('.', ',', '!', '?').ToLowerInvariant()[..1],
                random.Next(40, 100)))
            .ToList();

        return new PronunciationAssessment(
            overall,
            accuracy,
            fluency,
            completeness,
            prosody,
            phonemes,
            referenceText);
    }
}
