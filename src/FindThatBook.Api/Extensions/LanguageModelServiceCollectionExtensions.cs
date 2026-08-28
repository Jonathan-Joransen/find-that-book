using FindThatBook.Api.Providers;
using FindThatBook.Api.Providers.Gemini;
using FindThatBook.Api.Providers.OpenAI;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Extensions;

public static class LanguageModelServiceCollectionExtensions
{
    public static IServiceCollection AddLanguageModels(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LanguageModelOptions>()
            .Bind(configuration.GetSection(LanguageModelOptions.SectionName))
            .Validate(
                options => IsSupportedProvider(options.Provider),
                "LanguageModel:Provider must be Gemini or OpenAI.")
            .ValidateOnStart();

        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "Gemini:Model is required.")
            .ValidateOnStart();

        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "OpenAI:Model is required.")
            .ValidateOnStart();

        services.AddSingleton<GeminiLanguageModelProvider>();
        services.AddSingleton<OpenAiLanguageModelProvider>();
        services.AddSingleton<ILanguageModelProvider>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<LanguageModelOptions>>().Value;

            return options.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<GeminiLanguageModelProvider>()
                : provider.GetRequiredService<OpenAiLanguageModelProvider>();
        });

        return services;
    }

    private static bool IsSupportedProvider(string provider) =>
        provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase);
}
