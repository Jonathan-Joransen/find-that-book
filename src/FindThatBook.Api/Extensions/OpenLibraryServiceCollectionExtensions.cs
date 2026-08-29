using System.Net.Http.Headers;
using FindThatBook.Api.Providers.BookProviders;
using FindThatBook.Api.Providers.BookProviders.OpenLibrary;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace FindThatBook.Api.Extensions;

public static class OpenLibraryServiceCollectionExtensions
{
    public static IServiceCollection AddOpenLibrary(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var openLibraryConfiguration = configuration.GetSection(OpenLibraryOptions.SectionName);
        var retryCount = openLibraryConfiguration.GetValue(
            nameof(OpenLibraryOptions.RetryCount),
            new OpenLibraryOptions().RetryCount);
        var retryDelayMilliseconds = openLibraryConfiguration.GetValue(
            nameof(OpenLibraryOptions.RetryDelayMilliseconds),
            new OpenLibraryOptions().RetryDelayMilliseconds);

        services.AddOptions<OpenLibraryOptions>()
            .Bind(openLibraryConfiguration)
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "OpenLibrary:BaseUrl must be an absolute URL.")
            .Validate(
                options => options.SearchLimit is >= 1 and <= 100,
                "OpenLibrary:SearchLimit must be between 1 and 100.")
            .Validate(
                options => options.SearchCacheDurationMinutes is >= 1 and <= 1440,
                "OpenLibrary:SearchCacheDurationMinutes must be between 1 and 1440.")
            .Validate(
                options => options.RetryCount is >= 0 and <= 5,
                "OpenLibrary:RetryCount must be between 0 and 5.")
            .Validate(
                options => options.RetryDelayMilliseconds is >= 0 and <= 5000,
                "OpenLibrary:RetryDelayMilliseconds must be between 0 and 5000.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent),
                "OpenLibrary:UserAgent is required.")
            .ValidateOnStart();

        services.AddHybridCache();
        services.AddTransient<IBookProvider, CachedOpenLibraryBookProvider>();

        services
            .AddHttpClient<OpenLibraryBookProvider>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;

                client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = retryCount;
                options.Retry.Delay = TimeSpan.FromMilliseconds(retryDelayMilliseconds);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.Retry.DisableForUnsafeHttpMethods();
            });

        return services;
    }
}
