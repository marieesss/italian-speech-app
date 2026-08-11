using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using ItalianApp.Api.Features.Practice;
using ItalianApp.Api.Features.Progress;
using ItalianApp.Api.Infrastructure.Persistence;
using ItalianApp.Api.Infrastructure.Speech;
using ItalianApp.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItalianApp.Api.Tests.Practice;

[Collection(DatabaseCollection.Name)]
public class PracticeEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Session_requires_authentication()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);

        var response = await factory.CreateClient().GetAsync($"/api/practice/session?scenarioId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_scenario_is_not_found()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var client = await TestClient.AuthenticatedAsync(factory);

        var response = await client.GetAsync($"/api/practice/session?scenarioId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_fresh_session_serves_unattempted_phrases()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 3);
        var client = await TestClient.AuthenticatedAsync(factory);

        var session = await client.GetFromJsonAsync<SessionResponse>(
            $"/api/practice/session?scenarioId={seeded.Scenario.Id}");

        session!.Phrases.Should().HaveCount(3);
        session.Phrases.Should().OnlyContain(phrase => phrase.Bucket == "Fresh");
    }

    [Fact]
    public async Task Phrases_without_model_audio_are_left_out()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 2);
        await MutateAsync(factory, async db =>
        {
            var phrase = await db.Phrases.FirstAsync(row => row.ScenarioId == seeded.Scenario.Id);
            phrase.AudioUrl = null;
        });
        var client = await TestClient.AuthenticatedAsync(factory);

        var session = await client.GetFromJsonAsync<SessionResponse>(
            $"/api/practice/session?scenarioId={seeded.Scenario.Id}");

        session!.Phrases.Should().HaveCount(1);
    }

    [Fact]
    public async Task An_attempt_returns_a_score_and_feedback()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var attempt = await SubmitAsync(client, phraseId);

        attempt.OverallScore.Should().BeInRange(0, 100);
        attempt.FeedbackText.Should().NotBeEmpty();
        attempt.FeedbackSource.Should().Be("rules");
    }

    [Fact]
    public async Task A_failing_attempt_is_rescheduled_immediately()
    {
        using var factory = ScriptedFactory(score: 40);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var attempt = await SubmitAsync(client, phraseId);

        attempt.Passed.Should().BeFalse();
        attempt.NextReviewAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_passing_attempt_is_pushed_out_a_week()
    {
        using var factory = ScriptedFactory(score: 88);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var attempt = await SubmitAsync(client, phraseId);

        attempt.Passed.Should().BeTrue();
        attempt.NextReviewAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_failed_phrase_comes_back_at_the_top_of_the_session()
    {
        using var factory = ScriptedFactory(score: 35);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 3);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        await SubmitAsync(client, phraseId);

        var session = await client.GetFromJsonAsync<SessionResponse>(
            $"/api/practice/session?scenarioId={seeded.Scenario.Id}");

        session!.Phrases[0].Id.Should().Be(phraseId);
        session.Phrases[0].Bucket.Should().Be("Weak");
        session.Phrases[0].LastScore.Should().Be(35);
    }

    [Fact]
    public async Task The_attempt_is_persisted_without_any_audio()
    {
        using var factory = ScriptedFactory(score: 61);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var response = await SubmitAsync(client, phraseId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attempt = await db.Attempts.SingleAsync(row => row.Id == response.AttemptId);

        attempt.OverallScore.Should().Be(61);
        attempt.FeedbackSource.Should().Be(FeedbackSource.Rules);
        attempt.PhonemeScores.Should().NotBeNull();
    }

    [Fact]
    public async Task Scoring_quota_blocks_the_session_with_an_explicit_message()
    {
        using var factory = new IntegrationFactory(
            postgres.ConnectionString,
            new Dictionary<string, string?> { ["Quota:ScoringCallsPerDay"] = "1" });

        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 2);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        await SubmitAsync(client, phraseId);
        var blocked = await PostAudioAsync(client, phraseId);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await blocked.Content.ReadAsStringAsync();
        body.Should().Contain("minuit");
    }

    [Fact]
    public async Task Llm_quota_exhaustion_does_not_interrupt_practice()
    {
        using var factory = new IntegrationFactory(
            postgres.ConnectionString,
            new Dictionary<string, string?> { ["Quota:LlmCallsPerDay"] = "0" });

        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var attempt = await SubmitAsync(client, phraseId);

        attempt.FeedbackText.Should().NotBeEmpty();
        attempt.FeedbackSource.Should().Be("rules");
    }

    [Fact]
    public async Task A_request_without_an_audio_part_is_rejected()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        using var content = new MultipartFormDataContent
        {
            { new StringContent("oops"), "notaudio", "notaudio.txt" }
        };

        var response = await client.PostAsync($"/api/practice/phrases/{phraseId}/attempts", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_non_multipart_request_is_rejected()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var response = await client.PostAsJsonAsync($"/api/practice/phrases/{phraseId}/attempts", new { });

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task An_unreviewed_phrase_cannot_be_attempted()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 0, unreviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var response = await PostAudioAsync(client, phraseId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Targeted_advice_reaches_the_response()
    {
        using var factory = ScriptedFactory(
            score: 45,
            phonemes: [new PhonemeScore("caffè", "f", 20)]);

        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        var attempt = await SubmitAsync(client, phraseId);

        // CatalogSeed annotates every phrase with double_consonant:ff and final_vowel.
        attempt.Advice.Select(advice => advice.Code)
            .Should().Contain("double_consonant");
        attempt.Advice.Single(advice => advice.Code == "double_consonant").Focus.Should().Be("ff");
        attempt.WeakPhonemes.Should().ContainSingle().Which.Word.Should().Be("caffè");
    }

    [Fact]
    public async Task A_realistic_sized_recording_goes_through()
    {
        using var factory = new IntegrationFactory(postgres.ConnectionString);
        var seeded = await CatalogSeed.InsertAsync(factory, reviewedPhrases: 1);
        var client = await TestClient.AuthenticatedAsync(factory);
        var phraseId = await FirstPhraseIdAsync(factory, seeded.Scenario.Id);

        // 4 seconds of 16 kHz mono 16-bit. Well past the 64 kB threshold at which model
        // binding would have spooled the upload to a temp file.
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[128 * 1024]), "audio", "attempt.wav" }
        };

        var response = await client.PostAsync($"/api/practice/phrases/{phraseId}/attempts", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private IntegrationFactory ScriptedFactory(double score, PhonemeScore[]? phonemes = null) =>
        new(postgres.ConnectionString, null, services =>
        {
            services.AddSingleton(new FakePronunciationScorer
            {
                Script = text => new PronunciationAssessment(
                    score, score, score, 100, score, phonemes ?? [], text)
            });
            services.AddSingleton<IPronunciationScorer>(provider =>
                provider.GetRequiredService<FakePronunciationScorer>());
        });

    private static async Task<AttemptResponse> SubmitAsync(HttpClient client, Guid phraseId)
    {
        var response = await PostAudioAsync(client, phraseId);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AttemptResponse>())!;
    }

    private static Task<HttpResponseMessage> PostAudioAsync(HttpClient client, Guid phraseId)
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("RIFF....WAVEfmt ")), "audio", "attempt.wav" }
        };

        return client.PostAsync($"/api/practice/phrases/{phraseId}/attempts", content);
    }

    private static async Task<Guid> FirstPhraseIdAsync(IntegrationFactory factory, Guid scenarioId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Phrases
            .Where(phrase => phrase.ScenarioId == scenarioId)
            .OrderBy(phrase => phrase.TextIt)
            .Select(phrase => phrase.Id)
            .FirstAsync();
    }

    private static async Task MutateAsync(IntegrationFactory factory, Func<AppDbContext, Task> mutate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await mutate(db);
        await db.SaveChangesAsync();
    }
}
