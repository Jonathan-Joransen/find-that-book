using System.Net;
using System.Text;
using FindThatBook.Api.Extensions;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Providers;
using FindThatBook.Api.Providers.OpenLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FindThatBook.Api.Tests.Providers;

public sealed class OpenLibraryBookProviderTests
{
    [Fact]
    public async Task SearchAsync_MapsCurrentOpenLibrarySearchResponse()
    {
        const string json = """
            {
              "numFound": 1,
              "start": 0,
              "numFoundExact": true,
              "docs": [
                {
                  "key": "/works/OL102749W",
                  "title": "Moby Dick",
                  "author_name": ["Herman Melville"],
                  "first_publish_year": 1851,
                  "first_sentence": ["Call me Ishmael."],
                  "cover_i": 10521270
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var provider = CreateProvider(handler, searchLimit: 5);

        var books = await provider.SearchAsync(
            new BookSearchCompletion("  Moby Dick  ", null, null));

        var book = Assert.Single(books);
        Assert.Equal("Moby Dick", book.Title);
        Assert.Equal("Herman Melville", book.Author);
        Assert.Equal(1851, book.FirstPublishYear);
        Assert.Equal("Call me Ishmael.", book.Description);
        Assert.Equal("/works/OL102749W", book.BookKey);
        Assert.Equal("https://openlibrary.org/works/OL102749W", book.BookUrl);
        Assert.Equal(10521270, book.CoverId);
        Assert.Equal(
            "https://covers.openlibrary.org/b/id/10521270-M.jpg",
            book.CoverImageUrl);
        Assert.Equal("Strong title match.", book.Explanation);
        Assert.Equal(HttpMethod.Get, handler.Request?.Method);
        Assert.Equal(
            "?title=Moby%20Dick&fields=key%2Ctitle%2Cauthor_name%2Cfirst_publish_year%2Cfirst_sentence%2Ccover_i&limit=5",
            handler.Request?.RequestUri?.Query);
    }

    [Fact]
    public async Task SearchAsync_UsesFallbacksForOptionalOpenLibraryFields()
    {
        const string json = """
            {
              "docs": [
                {
                  "title": "Anonymous work"
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var provider = CreateProvider(handler);

        var books = await provider.SearchAsync(
            new BookSearchCompletion(null, null, "anonymous"));

        var book = Assert.Single(books);
        Assert.Equal("Unknown author", book.Author);
        Assert.Null(book.FirstPublishYear);
        Assert.Empty(book.Description);
        Assert.Null(book.BookKey);
        Assert.Null(book.BookUrl);
        Assert.Null(book.CoverId);
        Assert.Null(book.CoverImageUrl);
        Assert.Equal("The title shares distinctive terms with the query.", book.Explanation);
        Assert.Equal(
            "?q=anonymous&fields=key%2Ctitle%2Cauthor_name%2Cfirst_publish_year%2Cfirst_sentence%2Ccover_i&limit=12",
            handler.Request?.RequestUri?.Query);
    }

    [Fact]
    public async Task SearchAsync_ExplainsCombinedTitleAndAuthorMatch()
    {
        const string json = """
            {
              "docs": [
                {
                  "key": "OL262758W",
                  "title": "The Hobbit",
                  "author_name": ["J. R. R. Tolkien"],
                  "first_publish_year": 1937
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, json);
        var provider = CreateProvider(handler);

        var books = await provider.SearchAsync(
            new BookSearchCompletion("The Hobbit", "J.R.R. Tolkien", null));

        var book = Assert.Single(books);
        Assert.Equal("/works/OL262758W", book.BookKey);
        Assert.Equal("Strong title and primary-author match.", book.Explanation);
        Assert.Equal(
            "?title=The%20Hobbit&author=J.R.R.%20Tolkien&fields=key%2Ctitle%2Cauthor_name%2Cfirst_publish_year%2Cfirst_sentence%2Ccover_i&limit=12",
            handler.Request?.RequestUri?.Query);
    }

    [Fact]
    public async Task SearchAsync_ThrowsWhenOpenLibraryReturnsAnError()
    {
        var provider = CreateProvider(
            new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}"));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(
            new BookSearchCompletion("moby dick", null, null)));
    }

    [Fact]
    public async Task SearchAsync_ThrowsWhenOpenLibraryReturnsInvalidJson()
    {
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.OK, "not-json"));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(
            new BookSearchCompletion("moby dick", null, null)));
    }

    [Fact]
    public async Task SearchAsync_RetriesTransientFailureAndReturnsRecoveredResponse()
    {
        const string json = """
            {
              "docs": [
                {
                  "title": "Recovered book"
                }
              ]
            }
            """;
        var handler = new SequenceHttpMessageHandler(
            (HttpStatusCode.ServiceUnavailable, "{}"),
            (HttpStatusCode.OK, json));
        await using var serviceProvider = CreateServiceProvider(handler);
        var provider = serviceProvider.GetRequiredService<IBookProvider>();

        var books = await provider.SearchAsync(
            new BookSearchCompletion(null, null, "recovered"));

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal("Recovered book", Assert.Single(books).Title);
    }

    [Fact]
    public async Task SearchAsync_DoesNotRetryPermanentFailure()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, "{}");
        await using var serviceProvider = CreateServiceProvider(handler);
        var provider = serviceProvider.GetRequiredService<IBookProvider>();

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(
            new BookSearchCompletion(null, null, "bad request")));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SearchAsync_StopsAfterConfiguredRetryCount()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}");
        await using var serviceProvider = CreateServiceProvider(handler);
        var provider = serviceProvider.GetRequiredService<IBookProvider>();

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(
            new BookSearchCompletion(null, null, "unavailable")));

        Assert.Equal(3, handler.RequestCount);
    }

    private static OpenLibraryBookProvider CreateProvider(
        HttpMessageHandler handler,
        int searchLimit = 12)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openlibrary.org/")
        };
        var options = Options.Create(new OpenLibraryOptions
        {
            SearchLimit = searchLimit
        });

        return new OpenLibraryBookProvider(client, options);
    }

    private static ServiceProvider CreateServiceProvider(HttpMessageHandler handler)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{OpenLibraryOptions.SectionName}:BaseUrl"] = "https://openlibrary.org/",
            [$"{OpenLibraryOptions.SectionName}:SearchLimit"] = "12",
            [$"{OpenLibraryOptions.SectionName}:RetryCount"] = "2",
            [$"{OpenLibraryOptions.SectionName}:RetryDelayMilliseconds"] = "0",
            [$"{OpenLibraryOptions.SectionName}:UserAgent"] = "FindThatBook.Tests/1.0"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOpenLibrary(configuration);
        services
            .AddHttpClient<IBookProvider, OpenLibraryBookProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SequenceHttpMessageHandler(
        params (HttpStatusCode StatusCode, string ResponseBody)[] responses)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var index = Math.Min(RequestCount, responses.Length - 1);
            var response = responses[index];
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(
                    response.ResponseBody,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
