using FindThatBook.Api.Providers;
using FindThatBook.Api.Providers.Gemini;

namespace FindThatBook.Api.Extensions;

public static class LanguageModelServiceCollectionExtensions
{
    public static IServiceCollection AddLanguageModels(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "Gemini:Model is required.")
            .ValidateOnStart();

        services.AddSingleton<ILanguageModelProvider, GeminiLanguageModelProvider>();

        return services;
    }
}
