using System.Net.Http.Headers;
using FindThatBook.Api.Providers;
using FindThatBook.Api.Providers.OpenLibrary;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Extensions;

public static class OpenLibraryServiceCollectionExtensions
{
    public static IServiceCollection AddOpenLibrary(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OpenLibraryOptions>()
            .Bind(configuration.GetSection(OpenLibraryOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "OpenLibrary:BaseUrl must be an absolute URL.")
            .Validate(
                options => options.SearchLimit is >= 1 and <= 100,
                "OpenLibrary:SearchLimit must be between 1 and 100.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent),
                "OpenLibrary:UserAgent is required.")
            .ValidateOnStart();

        services.AddHttpClient<IBookProvider, OpenLibraryBookProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        });

        return services;
    }
}
