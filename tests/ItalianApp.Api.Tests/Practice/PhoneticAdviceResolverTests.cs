using FluentAssertions;
using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Practice;
using ItalianApp.Api.Infrastructure.Speech;

namespace ItalianApp.Api.Tests.Practice;

public class PhoneticAdviceResolverTests
{
    private static readonly Dictionary<string, PhoneticTip> Tips = new()
    {
        ["gli"] = Tip("gli", "Groupe gli", ["ʎ"], order: 50),
        ["gn"] = Tip("gn", "Groupe gn", ["ɲ"], order: 60),
        ["rolled_r"] = Tip("rolled_r", "R roulé", ["r", "ɾ"], order: 40),
        ["double_consonant"] = Tip("double_consonant", "Consonne double", [], order: 10),
        ["stress"] = Tip("stress", "Accent tonique", [], order: 20)
    };

    [Fact]
    public void A_weak_phoneme_pulls_its_tip()
    {
        // Passing overall, so only the phoneme match may speak.
        var resolved = Resolve(
            traps: ["gli", "gn"],
            phonemes: [("famiglia", "ʎ", 30)],
            overall: 95);

        resolved.Advice.Should().ContainSingle().Which.Code.Should().Be("gli");
    }

    [Fact]
    public void A_tip_the_phrase_is_not_annotated_with_is_never_shown()
    {
        var resolved = Resolve(
            traps: ["gn"],
            phonemes: [("famiglia", "ʎ", 30)],
            overall: 95);

        resolved.Advice.Should().BeEmpty();
    }

    [Fact]
    public void A_phoneme_above_the_threshold_triggers_nothing()
    {
        var resolved = Resolve(
            traps: ["gli"],
            phonemes: [("famiglia", "ʎ", 80)],
            overall: 95);

        resolved.Advice.Should().BeEmpty();
        resolved.WeakPhonemes.Should().BeEmpty();
    }

    [Fact]
    public void Worst_phoneme_leads()
    {
        var resolved = Resolve(
            traps: ["gli", "gn", "rolled_r"],
            phonemes: [("gnocchi", "ɲ", 55), ("ristorante", "r", 20), ("famiglia", "ʎ", 40)],
            overall: 95);

        resolved.Advice.Select(advice => advice.Code).Should().Equal("rolled_r", "gli", "gn");
    }

    [Fact]
    public void Weak_phonemes_are_reported_worst_first()
    {
        var resolved = Resolve(
            traps: [],
            phonemes: [("gnocchi", "ɲ", 55), ("ristorante", "r", 20)],
            overall: 95);

        resolved.WeakPhonemes.Select(weak => weak.Score).Should().Equal(20, 55);
    }

    [Fact]
    public void The_argument_is_carried_through()
    {
        var resolved = Resolve(
            traps: ["rolled_r:ristorante"],
            phonemes: [("ristorante", "r", 20)],
            overall: 95);

        resolved.Advice.Single().Argument.Should().Be("ristorante");
    }

    [Fact]
    public void Symbol_less_tips_surface_when_the_attempt_failed()
    {
        var resolved = Resolve(
            traps: ["double_consonant:tt", "stress:prenotàre"],
            phonemes: [],
            overall: 55);

        resolved.Advice.Select(advice => advice.Code).Should().Equal("double_consonant", "stress");
    }

    [Fact]
    public void Symbol_less_tips_stay_quiet_on_a_good_attempt()
    {
        var resolved = Resolve(
            traps: ["double_consonant:tt", "stress:prenotàre"],
            phonemes: [],
            overall: 88);

        resolved.Advice.Should().BeEmpty();
    }

    [Fact]
    public void Phoneme_matches_outrank_the_top_up()
    {
        var resolved = Resolve(
            traps: ["double_consonant:tt", "gli"],
            phonemes: [("famiglia", "ʎ", 25)],
            overall: 55);

        resolved.Advice.Select(advice => advice.Code).Should().Equal("gli", "double_consonant");
    }

    [Fact]
    public void No_more_than_three_tips_are_returned()
    {
        var resolved = Resolve(
            traps: ["gli", "gn", "rolled_r", "double_consonant", "stress"],
            phonemes: [("gnocchi", "ɲ", 10), ("ristorante", "r", 20), ("famiglia", "ʎ", 30)],
            overall: 40);

        resolved.Advice.Should().HaveCount(3);
    }

    [Fact]
    public void An_unknown_trap_code_is_ignored()
    {
        var resolved = Resolve(
            traps: ["not_a_real_code", "gli"],
            phonemes: [("famiglia", "ʎ", 30)],
            overall: 40);

        resolved.Advice.Select(advice => advice.Code).Should().Equal("gli");
    }

    [Fact]
    public void Phoneme_matching_ignores_case()
    {
        var resolved = Resolve(
            traps: ["rolled_r"],
            phonemes: [("ristorante", "R", 20)],
            overall: 95);

        resolved.Advice.Should().ContainSingle();
    }

    private static ResolvedAdvice Resolve(
        string[] traps,
        (string Word, string Phoneme, double Score)[] phonemes,
        double overall = 60)
    {
        var assessment = new PronunciationAssessment(
            overall,
            overall,
            overall,
            100,
            overall,
            phonemes.Select(item => new PhonemeScore(item.Word, item.Phoneme, item.Score)).ToList(),
            "recognised");

        return PhoneticAdviceResolver.Resolve(traps, assessment, Tips, new PracticeOptions());
    }

    private static PhoneticTip Tip(string code, string label, List<string> symbols, int order) => new()
    {
        Code = code,
        LabelFr = label,
        AdviceFr = $"Conseil pour {code}.",
        PhonemeSymbols = symbols,
        DisplayOrder = order
    };
}
