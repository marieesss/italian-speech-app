using FluentAssertions;
using ItalianApp.Api.Infrastructure.Llm;
using ItalianApp.Api.Infrastructure.Speech;

namespace ItalianApp.Api.Tests.Llm;

public class RuleBasedFeedbackWriterTests
{
    private readonly RuleBasedFeedbackWriter _writer = new();

    [Theory]
    [InlineData(95, "Très bien")]
    [InlineData(80, "Bien")]
    [InlineData(65, "compréhensible")]
    [InlineData(40, "Réécoute le modèle")]
    public async Task Opening_follows_the_overall_score(double overall, string expected)
    {
        var feedback = await Write(Assessment(overall));

        feedback.Text.Should().Contain(expected);
    }

    [Fact]
    public async Task Feedback_is_never_marked_as_coming_from_the_llm()
    {
        var feedback = await Write(Assessment(80));

        feedback.FromLlm.Should().BeFalse();
    }

    [Fact]
    public async Task Advice_argument_is_quoted_and_the_sentence_lowercased()
    {
        var feedback = await Write(
            Assessment(70),
            new PhoneticAdvice("double_consonant", "Consonne double", "En italien, la consonne double se tient plus longtemps.", "tt"));

        feedback.Text.Should().Contain("Sur « tt » : en italien, la consonne double");
    }

    [Fact]
    public async Task Advice_without_argument_is_used_verbatim()
    {
        var feedback = await Write(
            Assessment(70),
            new PhoneticAdvice("gli", "Groupe gli", "Le groupe « gli » se prononce comme le « ill » de « famille ».", null));

        feedback.Text.Should().Contain("Le groupe « gli » se prononce");
    }

    [Fact]
    public async Task At_most_three_tips_are_kept()
    {
        var tips = Enumerable.Range(0, 7)
            .Select(index => new PhoneticAdvice($"code{index}", "label", $"Conseil numéro {index}.", null))
            .ToArray();

        var feedback = await Write(Assessment(70), tips);

        var kept = tips.Count(tip => feedback.Text.Contains(tip.AdviceFr, StringComparison.Ordinal));
        kept.Should().Be(3);
    }

    [Fact]
    public async Task Missing_words_are_called_out()
    {
        var feedback = await Write(Assessment(70, completeness: 55));

        feedback.Text.Should().Contain("escamotés");
    }

    [Fact]
    public async Task Completeness_wins_over_fluency()
    {
        var feedback = await Write(Assessment(70, completeness: 55, fluency: 30));

        feedback.Text.Should().Contain("escamotés");
        feedback.Text.Should().NotContain("hésitant");
    }

    [Fact]
    public async Task Hesitant_delivery_is_called_out_when_nothing_is_missing()
    {
        var feedback = await Write(Assessment(70, completeness: 100, fluency: 30));

        feedback.Text.Should().Contain("hésitant");
    }

    [Fact]
    public async Task Clean_attempt_gets_no_closing_remark()
    {
        var feedback = await Write(Assessment(95, completeness: 100, fluency: 95));

        feedback.Text.Should().NotContain("escamotés").And.NotContain("hésitant");
    }

    [Fact]
    public async Task Score_is_rendered_as_a_rounded_percentage()
    {
        var feedback = await Write(Assessment(87.4));

        feedback.Text.Should().Contain("87 %");
    }

    private Task<Feedback> Write(PronunciationAssessment assessment, params PhoneticAdvice[] advice) =>
        _writer.WriteAsync(new FeedbackRequest("Un caffè per favore", "Un café s'il vous plaît", assessment, advice));

    private static PronunciationAssessment Assessment(
        double overall,
        double completeness = 100,
        double fluency = 90) =>
        new(overall, overall, fluency, completeness, overall, [], "Un caffè per favore");
}
