using System.Text;
using FluentAssertions;
using ItalianApp.Api.Infrastructure.Speech;

namespace ItalianApp.Api.Tests.Speech;

public class FakePronunciationScorerTests
{
    [Fact]
    public async Task Same_text_always_scores_the_same()
    {
        var first = await Score("Un caffè per favore");
        var second = await Score("Un caffè per favore");

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task Different_texts_score_differently()
    {
        var first = await Score("Un caffè per favore");
        var second = await Score("Vorrei prenotare un tavolo");

        second.OverallScore.Should().NotBe(first.OverallScore);
    }

    [Fact]
    public async Task Every_word_gets_a_phoneme_score()
    {
        var assessment = await Score("Vorrei prenotare un tavolo");

        assessment.PhonemeScores.Should().HaveCount(4);
        assessment.PhonemeScores.Select(score => score.Score).Should().OnlyContain(score => score >= 0 && score <= 100);
    }

    [Fact]
    public async Task The_audio_stream_is_consumed_and_not_kept()
    {
        var scorer = new FakePronunciationScorer();
        using var audio = new MemoryStream(Encoding.UTF8.GetBytes("pretend-this-is-wav"));

        await scorer.ScoreAsync(audio, "Un caffè per favore");

        audio.Position.Should().Be(audio.Length);
    }

    [Fact]
    public async Task Script_overrides_the_derived_scores()
    {
        var scorer = new FakePronunciationScorer
        {
            Script = _ => new PronunciationAssessment(12, 12, 12, 12, 12, [], "nope")
        };

        var assessment = await scorer.ScoreAsync(Stream.Null, "Un caffè per favore");

        assessment.OverallScore.Should().Be(12);
    }

    [Fact]
    public async Task Calls_are_counted()
    {
        var scorer = new FakePronunciationScorer();

        await scorer.ScoreAsync(Stream.Null, "uno");
        await scorer.ScoreAsync(Stream.Null, "due");

        scorer.CallCount.Should().Be(2);
    }

    private static async Task<PronunciationAssessment> Score(string text) =>
        await new FakePronunciationScorer().ScoreAsync(Stream.Null, text);
}

public class FakeTextToSpeechTests
{
    [Fact]
    public async Task Output_starts_with_an_id3_header()
    {
        var bytes = await new FakeTextToSpeech().SynthesiseAsync("Un caffè", "it-IT-ElsaNeural");

        Encoding.ASCII.GetString(bytes, 0, 3).Should().Be("ID3");
    }

    [Fact]
    public async Task Different_phrases_produce_different_bytes()
    {
        var tts = new FakeTextToSpeech();

        var first = await tts.SynthesiseAsync("Un caffè", "it-IT-ElsaNeural");
        var second = await tts.SynthesiseAsync("Un tavolo", "it-IT-ElsaNeural");

        second.Should().NotEqual(first);
    }

    [Fact]
    public async Task Calls_are_recorded_with_their_voice()
    {
        var tts = new FakeTextToSpeech();

        await tts.SynthesiseAsync("Un caffè", "it-IT-ElsaNeural");

        tts.Calls.Should().ContainSingle().Which.Should().Be(("Un caffè", "it-IT-ElsaNeural"));
    }
}
