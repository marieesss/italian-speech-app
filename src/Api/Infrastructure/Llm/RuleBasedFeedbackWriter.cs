using System.Globalization;
using System.Text;

namespace ItalianApp.Api.Infrastructure.Llm;

// Production fallback, not just a test double: it runs whenever Claude is unavailable
// or the daily LLM quota is spent. The advice text itself comes from the database, so
// this only handles ordering and connective tissue.
public class RuleBasedFeedbackWriter : IFeedbackWriter
{
    private const int MaxAdvice = 3;
    private const double LowCompleteness = 80;
    private const double LowFluency = 60;

    public Task<Feedback> WriteAsync(FeedbackRequest request, CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();

        text.Append(Opening(request.Assessment.OverallScore));

        foreach (var advice in request.Advice.Take(MaxAdvice))
        {
            text.Append(' ').Append(Sentence(advice));
        }

        var closing = Closing(request);
        if (closing is not null)
        {
            text.Append(' ').Append(closing);
        }

        return Task.FromResult(new Feedback(text.ToString(), FromLlm: false));
    }

    private static string Opening(double overallScore) => overallScore switch
    {
        >= 90 => $"Très bien : {Percent(overallScore)}, c'est proche du modèle.",
        >= 75 => $"Bien : {Percent(overallScore)}. La phrase passe, il reste des détails.",
        >= 60 => $"{Percent(overallScore)}. C'est compréhensible, mais quelques sons sont à reprendre.",
        _ => $"{Percent(overallScore)}. Réécoute le modèle avant de réessayer."
    };

    private static string Sentence(PhoneticAdvice advice) => advice.Argument is null
        ? advice.AdviceFr
        : $"Sur « {advice.Argument} » : {Lower(advice.AdviceFr)}";

    private static string? Closing(FeedbackRequest request)
    {
        // Completeness first: a missing word matters more than a hesitant delivery.
        if (request.Assessment.CompletenessScore < LowCompleteness)
        {
            return "Des mots ont été escamotés : reprends la phrase en entier.";
        }

        if (request.Assessment.FluencyScore < LowFluency)
        {
            return "Le débit est hésitant. Réécoute le modèle et enchaîne sans t'arrêter.";
        }

        return null;
    }

    private static string Percent(double score) =>
        Math.Round(score).ToString("0", CultureInfo.InvariantCulture) + " %";

    private static string Lower(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToLowerInvariant(sentence[0]) + sentence[1..];
}
