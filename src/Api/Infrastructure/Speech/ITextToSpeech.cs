namespace ItalianApp.Api.Infrastructure.Speech;

public interface ITextToSpeech
{
    // Only ever called by seed-audio. Nothing on the request path may reach this.
    Task<byte[]> SynthesiseAsync(
        string text,
        string voice,
        CancellationToken cancellationToken = default);
}
