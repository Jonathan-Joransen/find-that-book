using FindThatBook.Api.Providers.Gemini;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FindThatBook.Api.Tests.Integration;

internal sealed class GeminiIntegrationTheoryAttribute : TheoryAttribute
{
    public GeminiIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(GeminiIntegrationTestConfiguration.GetOptions().ApiKey))
        {
            Skip = "Configure Gemini:ApiKey in user secrets or GEMINI_API_KEY to run live Gemini tests.";
        }
    }
}

internal static class GeminiIntegrationTestConfiguration
{
    public static GeminiOptions GetOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(GeminiOptions).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var configuredOptions = configuration
            .GetSection(GeminiOptions.SectionName)
            .Get<GeminiOptions>();

        configuredOptions ??= new GeminiOptions();

        return new GeminiOptions
        {
            ApiKey = configuredOptions.ApiKey
                ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            Model = configuredOptions.Model
        };
    }
}
