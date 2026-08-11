using ItalianApp.Api.Infrastructure.Speech;

namespace ItalianApp.Api.Infrastructure.Llm;

// One tip resolved against the phrase's annotations. Argument pins the occurrence,
// e.g. Code=double_consonant, Argument=tt.
public record PhoneticAdvice(string Code, string LabelFr, string AdviceFr, string? Argument);

public record FeedbackRequest(
    string TextIt,
    string TextFr,
    PronunciationAssessment Assessment,
    IReadOnlyList<PhoneticAdvice> Advice);

public record Feedback(string Text, bool FromLlm);

public interface IFeedbackWriter
{
    Task<Feedback> WriteAsync(FeedbackRequest request, CancellationToken cancellationToken = default);
}
