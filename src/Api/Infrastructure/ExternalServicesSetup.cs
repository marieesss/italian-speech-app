using ItalianApp.Api.Infrastructure.Llm;
using ItalianApp.Api.Infrastructure.Speech;

namespace ItalianApp.Api.Infrastructure;

public static class ExternalServicesSetup
{
    // Only the fakes exist so far; the Azure and Claude implementations land with their
    // own commit, and selection then keys off whether credentials are configured.
    public static IServiceCollection AddExternalServices(this IServiceCollection services)
    {
        services.AddSingleton<FakePronunciationScorer>();
        services.AddSingleton<IPronunciationScorer>(provider =>
            provider.GetRequiredService<FakePronunciationScorer>());

        services.AddSingleton<FakeTextToSpeech>();
        services.AddSingleton<ITextToSpeech>(provider =>
            provider.GetRequiredService<FakeTextToSpeech>());

        services.AddSingleton<RuleBasedFeedbackWriter>();
        services.AddSingleton<IFeedbackWriter>(provider =>
            provider.GetRequiredService<RuleBasedFeedbackWriter>());

        return services;
    }
}
