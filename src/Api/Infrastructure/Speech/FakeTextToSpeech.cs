using System.Text;

namespace ItalianApp.Api.Infrastructure.Speech;

public class FakeTextToSpeech : ITextToSpeech
{
    public List<(string Text, string Voice)> Calls { get; } = [];

    public Task<byte[]> SynthesiseAsync(
        string text,
        string voice,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((text, voice));

        // An ID3v2 header followed by the text, so seed-audio tests can tell files apart
        // and a byte-length assertion means something.
        var payload = new List<byte>("ID3"u8.ToArray());
        payload.AddRange([0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        payload.AddRange(Encoding.UTF8.GetBytes($"{voice}|{text}"));

        return Task.FromResult(payload.ToArray());
    }
}
