namespace ItalianApp.Api.Features.Quota;

public enum QuotaKind
{
    Scoring,
    Llm,
    Tts
}

public class QuotaOptions
{
    public const string SectionName = "Quota";

    public int ScoringCallsPerDay { get; set; } = 150;
    public int LlmCallsPerDay { get; set; } = 100;

    // Zero, and meant to stay zero: model audio is pre-generated.
    public int TtsCallsPerDay { get; set; }

    // Counters reset at local midnight, not UTC — "one session a day" has to mean the
    // learner's day.
    public string TimeZone { get; set; } = "Europe/Paris";

    public int LimitFor(QuotaKind kind) => kind switch
    {
        QuotaKind.Scoring => ScoringCallsPerDay,
        QuotaKind.Llm => LlmCallsPerDay,
        QuotaKind.Tts => TtsCallsPerDay,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
