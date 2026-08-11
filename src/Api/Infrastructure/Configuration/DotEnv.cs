namespace ItalianApp.Api.Infrastructure.Configuration;

public static class DotEnv
{
    // UPPER_SNAKE names don't map unambiguously onto nested configuration keys,
    // so the mapping is spelled out. Adding a setting means adding a line here.
    private static readonly Dictionary<string, string> Keys = new(StringComparer.Ordinal)
    {
        ["DB_CONNECTION"] = "ConnectionStrings:Default",

        ["JWT_SIGNING_SECRET"] = "Jwt:SigningSecret",
        ["JWT_ISSUER"] = "Jwt:Issuer",
        ["JWT_AUDIENCE"] = "Jwt:Audience",
        ["JWT_LIFETIME_HOURS"] = "Jwt:LifetimeHours",

        ["IDENTITY_ALLOW_REGISTRATION"] = "Identity:AllowRegistration",

        ["AZURE_SPEECH_SUBSCRIPTION"] = "Azure:Speech:Subscription",
        ["AZURE_SPEECH_REGION"] = "Azure:Speech:Region",
        ["AZURE_SPEECH_ITALIAN_VOICE"] = "Azure:Speech:ItalianVoice",

        ["ANTHROPIC_TOKEN"] = "Anthropic:Token",
        ["ANTHROPIC_MODEL"] = "Anthropic:Model",

        ["QUOTA_SCORING_CALLS_PER_DAY"] = "Quota:ScoringCallsPerDay",
        ["QUOTA_LLM_CALLS_PER_DAY"] = "Quota:LlmCallsPerDay",
        ["QUOTA_TTS_CALLS_PER_DAY"] = "Quota:TtsCallsPerDay"
    };

    public static IConfigurationBuilder AddDotEnv(this IConfigurationBuilder builder, string startDirectory)
    {
        var fromFile = ReadFile(Locate(startDirectory));
        var settings = new Dictionary<string, string?>();

        foreach (var (name, key) in Keys)
        {
            // A real environment variable wins over the file.
            var value = Environment.GetEnvironmentVariable(name)
                        ?? (fromFile.TryGetValue(name, out var fileValue) ? fileValue : null);

            if (!string.IsNullOrWhiteSpace(value))
            {
                settings[key] = value;
            }
        }

        return settings.Count == 0 ? builder : builder.AddInMemoryCollection(settings);
    }

    // Walks up from the content root so the API finds the .env at the repo root.
    private static string? Locate(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static Dictionary<string, string> ReadFile(string? path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (path is null)
        {
            return values;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            // Split on the first '=' only: connection strings contain more of them.
            values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
        }

        return values;
    }
}
