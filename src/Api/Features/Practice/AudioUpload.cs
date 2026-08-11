using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace ItalianApp.Api.Features.Practice;

// Reads the audio part straight off the network stream. Model binding to IFormFile would
// spool anything over 64 kB to a temp file, and a 4-second WAV is well past that — the
// attempt audio must never reach the disk.
public static class AudioUpload
{
    private const string PartName = "audio";
    private const int BoundaryLengthLimit = 128;

    public static bool IsMultipart(string? contentType) =>
        contentType is not null
        && contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase);

    public static async Task<T> ReadAsync<T>(
        HttpRequest request,
        Func<Stream, Task<T>> consume,
        CancellationToken cancellationToken)
    {
        var boundary = HeaderUtilities.RemoveQuotes(
            MediaTypeHeaderValue.Parse(request.ContentType).Boundary).Value;

        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > BoundaryLengthLimit)
        {
            throw new InvalidDataException("Missing or unusable multipart boundary.");
        }

        var reader = new MultipartReader(boundary, request.Body);

        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
            {
                continue;
            }

            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

            if (string.Equals(name, PartName, StringComparison.OrdinalIgnoreCase))
            {
                return await consume(section.Body);
            }
        }

        throw new InvalidDataException($"No '{PartName}' part in the request.");
    }
}
