using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Providers.LanguageModelProviders.Gemini;
using FindThatBook.Api.Services.BookFinding;

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
        services.AddScoped<IBookFinder, LanguageModelBookFinder>();

        return services;
    }
}
