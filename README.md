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
first publication year when known, Open Library work key and link, a cover URL
when available, and a concise explanation of the match. Open Library's response
models remain internal to the provider, and its default relevance ordering is
preserved.

Open Library settings are under `OpenLibrary` in
`src/FindThatBook.Api/appsettings.json`.
The Microsoft HTTP resilience pipeline retries transient timeouts, rate limits,
and server errors with exponential backoff and jitter according to `RetryCount`
and `RetryDelayMilliseconds`.
Before deploying an instance that sends regular traffic, update `UserAgent` to
include a contact email, as requested by Open Library's API usage guidelines.

## Gemini query refinement

`BookSearchService` sends a typed `BookSearchPrompt` to Gemini for every search. The resulting
nullable title, author, and keyword evidence is validated and sent to Open Library as separate
search fields for book metadata.
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

Live book-search prompt integration tests run when a Gemini API key is available
and are skipped when one is not configured. To run only these tests:

```bash
dotnet test FindThatBook.slnx --filter Category=Integration \
  --logger "console;verbosity=detailed"
```

The tests read `Gemini:ApiKey` from the API project's user secrets or
`GEMINI_API_KEY` from the environment. Set `Gemini__Model` to override the model.
