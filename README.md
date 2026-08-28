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
structured query is validated and then sent to Open Library for book metadata.
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
