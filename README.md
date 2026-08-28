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
`IBookProvider` implementation. Results preserve the frontend contract while
Open Library's response models remain internal to the provider.

Open Library settings are under `OpenLibrary` in
`src/FindThatBook.Api/appsettings.json`.
Before deploying an instance that sends regular traffic, update `UserAgent` to
include a contact email, as requested by Open Library's API usage guidelines.

## Gemini query refinement

Language-model query refinement is disabled by default, so local search works
without an API key. When enabled, `BookSearchService` sends a typed
`BookSearchPrompt` to Gemini. The resulting
structured query is validated and then sent to Open Library for book metadata.

Enable Gemini:

```bash
export LanguageModel__Enabled=true
export GEMINI_API_KEY=your-key
```

The model name can be overridden with `Gemini__Model`. API keys should be
supplied through environment variables or a local secret store and should never
be committed.

## Tests

```bash
dotnet test FindThatBook.slnx
```
