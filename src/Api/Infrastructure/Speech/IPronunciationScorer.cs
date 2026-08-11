namespace ItalianApp.Api.Infrastructure.Speech;

public record PhonemeScore(string Word, string Phoneme, double Score);

public record PronunciationAssessment(
    double OverallScore,
    double AccuracyScore,
    double FluencyScore,
    double CompletenessScore,
    double ProsodyScore,
    IReadOnlyList<PhonemeScore> PhonemeScores,
    string RecognisedText);

public interface IPronunciationScorer
{
    // The stream is read once and dropped. Implementations must not write it anywhere:
    // attempt audio is never persisted.
    Task<PronunciationAssessment> ScoreAsync(
        Stream audio,
        string referenceText,
        CancellationToken cancellationToken = default);
}
