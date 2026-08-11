using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Infrastructure.Llm;
using ItalianApp.Api.Infrastructure.Speech;

namespace ItalianApp.Api.Features.Practice;

public record WeakPhoneme(string Word, string Phoneme, double Score);

public record ResolvedAdvice(IReadOnlyList<PhoneticAdvice> Advice, IReadOnlyList<WeakPhoneme> WeakPhonemes);

// Crosses Azure's per-phoneme scores with the annotations a human wrote on the phrase.
// No LLM involved: the wording comes from PhoneticTips, so it is free and always correct.
public static class PhoneticAdviceResolver
{
    public static ResolvedAdvice Resolve(
        IReadOnlyList<string> phraseTraps,
        PronunciationAssessment assessment,
        IReadOnlyDictionary<string, PhoneticTip> tips,
        PracticeOptions options)
    {
        var weak = assessment.PhonemeScores
            .Where(score => score.Score < options.WeakPhonemeThreshold)
            .OrderBy(score => score.Score)
            .Select(score => new WeakPhoneme(score.Word, score.Phoneme, score.Score))
            .ToList();

        var traps = phraseTraps
            .Where(raw => !string.IsNullOrWhiteSpace(raw))
            .Select(PhoneticTrap.Parse)
            .Where(trap => tips.ContainsKey(trap.Code))
            .ToList();

        // A trap is triggered when one of its tip's phoneme symbols scored low. Worst
        // phoneme first, so the most useful advice leads.
        var triggered = new List<PhoneticTrap>();
        foreach (var phoneme in weak)
        {
            triggered.AddRange(traps
                .Where(trap => !triggered.Contains(trap))
                .Where(trap => tips[trap.Code].PhonemeSymbols
                    .Contains(phoneme.Phoneme, StringComparer.OrdinalIgnoreCase)));
        }

        // Tips like double_consonant or stress carry no phoneme symbol and can never be
        // triggered that way. When the attempt failed, the human annotation is the best
        // signal available, so the remaining traps top the list up.
        if (triggered.Count < options.MaxAdvice && assessment.OverallScore < options.PassingScore)
        {
            triggered.AddRange(traps
                .Except(triggered)
                .OrderBy(trap => tips[trap.Code].DisplayOrder));
        }

        var advice = triggered
            .Take(options.MaxAdvice)
            .Select(trap => new PhoneticAdvice(
                trap.Code,
                tips[trap.Code].LabelFr,
                tips[trap.Code].AdviceFr,
                trap.Argument))
            .ToList();

        return new ResolvedAdvice(advice, weak);
    }
}
