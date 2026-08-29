# Find That Book

A starter full-stack book search app with a .NET Web API and React frontend.

## Features completed

- Accepts sparse, noisy, and descriptive plain-text book queries up to 500 characters.
- Uses a bounded Gemini tool-calling workflow to interpret likely titles, authors, and
  distinctive keywords and to refine weak or ambiguous searches.
- Retrieves candidates from Open Library and enriches a bounded shortlist with canonical
  work and author data.
- Preserves structured author evidence, separates explicit contributor roles from primary
  work authors, and falls back safely when enrichment data is unavailable.
- Returns an ordered candidate list with title, author, publication year, Open Library link,
  cover when available, match score, and a grounded explanation.
- Provides a responsive React UI with loading, error, empty-result, and repeated-search states.
- Retries transient Open Library failures with bounded exponential backoff and jitter.
- Includes unit, controlled Gemini integration, and opt-in live external tests.

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

The endpoint trims the query, rejects blank values and values longer than 500 characters,
and searches Open Library through an
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

## Assumptions and design decisions

- This is a human-facing discovery workflow, so a trimmed query must contain between 1 and
  500 characters. The same upper bound is enforced by both the UI and API.
- Open Library's search ranking is useful for initial candidate retrieval, but its flattened
  author names are not considered verified primary-author evidence.
- A canonical work author with no role, or an explicit author/writer role, is treated as a
  primary author. Other explicit roles are retained as contributor evidence. Search-only
  author names remain marked as unverified.
- Canonical enrichment is limited to the first `WorkEnrichmentLimit` search results to bound
  latency and external requests. Failed enrichment does not fail an otherwise usable search.
- Gemini may reformulate retrieval and rank candidates, but it can return only opaque IDs for
  records supplied by the server. The application validates those IDs, scores, explanations,
  result count, and ordering.
- Scores are request-relative ranking signals rather than calibrated probabilities. The
  application returns only candidates scoring above 60 and no more than 12 results.

Additional rationale is documented in [tradeoffs.md](tradeoffs.md).

## Testing strategy

```bash
dotnet test FindThatBook.slnx
```

The default suite focuses on the boundaries with the highest correctness risk:

- API validation for blank, oversized, and maximum-length queries.
- Open Library request construction, response mapping, canonical author enrichment,
  contributor separation, fallback behavior, request-local caching, and retries.
- Gemini prompt/tool constraints, candidate identity, structured author evidence, output
  validation, score filtering, ordering, and result caps.
- Controlled Gemini scenarios for descriptive searches, partial titles, noisy mixed
  title/author text, ambiguous author-only searches, and primary-author versus contributor
  evidence.

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

## Known limitations and next improvements

- Gemini is currently required for every search; there is no deterministic retrieval and
  ranking fallback when the model is unavailable.
- De-duplication across different Open Library work keys is instructed and model-validated
  only indirectly; canonical cross-work grouping is not enforced by application code.
- Canonical work and author data can still be incomplete or incorrect, and enrichment is
  intentionally limited to the highest-ranked search results.
- Work and author lookups are cached only for the current provider session. A production
  deployment would add bounded cross-request caching and fuller request metrics.
- The UI currently presents a general search failure message rather than distinguishing
  validation, Gemini, and Open Library failures, and it does not yet have automated component
  or browser tests.
- More time would also go toward alias, diacritic, subtitle, translation, and misspelling
  normalization; deterministic evidence scoring; accessibility review; and a deployed demo.
