# Find That Book

A starter full-stack book search app with a .NET Web API and React frontend.

## Project structure

```text
src/
  FindThatBook.Api/       ASP.NET Core API
  FindThatBook.Web/       React and Vite frontend
tests/
  FindThatBook.Api.Tests/ API unit tests
```

## Requirements

- .NET 10 SDK
- Node.js 22.12+ or 24+
- npm 10+

## Run locally

Start the API:

```bash
cd src/FindThatBook.Api
dotnet run --launch-profile http
```

In a second terminal, start the frontend:

```bash
cd src/FindThatBook.Web
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `POST /book/search` to the API at
`http://localhost:5050` during local development.

## API

`POST /book/search`

```json
{
  "query": "classic novels about adventure"
}
```

The endpoint validates the query and searches Open Library through an
`IBookProvider` implementation. Each result includes its title, author names,
first publication year when known, a book key and link, a cover URL
when available, a match score, and a concise explanation of the match. Open Library's
response models remain internal to the provider. A bounded Gemini book-finder prompt
can search Open Library up to three times, pool the candidates, and adapt its query when
earlier results are weak. Only candidates scoring above 60 are returned, in descending
score order, with a maximum of 12 results.

Open Library settings are under `OpenLibrary` in
`src/FindThatBook.Api/appsettings.json`.
The Microsoft HTTP resilience pipeline retries transient timeouts, rate limits,
and server errors with exponential backoff and jitter according to `RetryCount`
and `RetryDelayMilliseconds`.
Before deploying an instance that sends regular traffic, update `UserAgent` to
include a contact email, as requested by Open Library's API usage guidelines.

When the API runs in the `Development` environment, information-level console logs
show each tool search, the typed Gemini response, and the raw Open Library responses.
The base configuration used by production and other environments raises the default
minimum level to `Warning`, so response bodies and normal search details are not logged.

## Gemini book finder

`LanguageModelBookFinder` sends a typed `BookFinderPrompt` to Gemini with a read-only
`search_open_library` tool. Gemini must use the tool at least once and may refine the search
up to three total calls. Tool results receive request-local candidate IDs; Gemini returns only
those IDs with scores and reasons, and the application joins them back to the original Open
Library records before returning them.

Configure the Gemini API key before starting the API:

```bash
export GEMINI_API_KEY=your-key
```

The language-model provider is selected in
`LanguageModelServiceCollectionExtensions`; changing providers is a code change.
The model name can be overridden with `Gemini__Model`. API keys should be
supplied through environment variables or a local secret store and should never
be committed.

## Tests

```bash
dotnet test FindThatBook.slnx
```

Controlled book-finder integration tests use live Gemini with deterministic candidate
books. They run when a Gemini API key is available and are skipped otherwise:

```bash
dotnet test FindThatBook.slnx --filter Category=Integration \
  --logger "console;verbosity=detailed"
```

An opt-in live-external suite sends curated reader descriptions through the complete
Gemini and Open Library workflow and verifies the expected books and response invariants:

```bash
RUN_LIVE_EXTERNAL_TESTS=true dotnet test FindThatBook.slnx \
  --filter Category=LiveExternal \
  --logger "console;verbosity=detailed"
```

The external suite is excluded by default because its results depend on two live services.
It currently covers descriptions for *Moby-Dick*, *Frankenstein*, and *The Hobbit* and
asserts the expected title and author, scores above 60, descending order, populated reasons,
and a maximum of 12 results.

The tests read `Gemini:ApiKey` from the API project's user secrets or
`GEMINI_API_KEY` from the environment. Set `Gemini__Model` to override the model.
