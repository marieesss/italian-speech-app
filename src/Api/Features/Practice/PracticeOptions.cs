namespace ItalianApp.Api.Features.Practice;

public class PracticeOptions
{
    public const string SectionName = "Practice";

    // Below this the phrase comes back in the same session.
    public double PassingScore { get; set; } = 70;

    // Azure phoneme score under which a targeted tip is worth showing. Provisional:
    // to be calibrated against real assessments before the catalogue is frozen.
    public double WeakPhonemeThreshold { get; set; } = 60;

    public int ReviewIntervalDays { get; set; } = 7;

    public int MaxAdvice { get; set; } = 3;

    public int DefaultSessionSize { get; set; } = 10;

    // 4 seconds of 16 kHz mono 16-bit is ~128 kB; this leaves room without inviting uploads.
    public long MaxAudioBytes { get; set; } = 2 * 1024 * 1024;
}
