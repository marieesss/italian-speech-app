namespace ItalianApp.Api.Features.Quota;

public static class QuotaSetup
{
    public static IServiceCollection AddQuotaFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<QuotaOptions>(configuration.GetSection(QuotaOptions.SectionName));
        services.AddScoped<IUsageMeter, UsageMeter>();

        return services;
    }
}
