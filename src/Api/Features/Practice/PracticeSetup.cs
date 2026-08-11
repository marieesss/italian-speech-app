namespace ItalianApp.Api.Features.Practice;

public static class PracticeSetup
{
    public static IServiceCollection AddPracticeFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PracticeOptions>(configuration.GetSection(PracticeOptions.SectionName));
        services.AddScoped<AttemptRecorder>();

        return services;
    }
}
