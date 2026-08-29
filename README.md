# Find That Book

## Quick start

### Requirements

- .NET 10 SDK
- Node.js 22.12+ or 24+
- npm 10+
- A Gemini API key

The commands below assume the current directory is the repository root.

### 1. Configure Gemini

Create an API key in [Google AI Studio](https://aistudio.google.com/app/apikey). For local
development, store it with .NET user secrets from the repository root:

```bash
dotnet user-secrets set "Gemini:ApiKey" "your-key" \
  --project src/FindThatBook.Api
```

### 2. Start the API

After configuring the API key, run:

```bash
cd src/FindThatBook.Api
dotnet run --launch-profile http
```

The API starts at `http://localhost:5050`.

### 3. Start the frontend

In a second terminal, from the repository root, run:

```bash
cd src/FindThatBook.Web
npm install
npm run dev
```

Open the URL it creates, likely `http://localhost:5173`.

## Implementation approach

The application uses AI for the ambiguous parts of book discovery while keeping data
retrieval, validation, and response shaping deterministic.

1. The React UI sends the reader's plain-text description to `POST /book/search`.
2. The API validates and caches the query, then gives the original text to Gemini with a
   constrained Open Library search tool. Gemini interprets the text as a possible title,
   author, or set of distinctive keywords and may make up to three meaningfully different
   searches when the first results are weak or ambiguous.
3. The server normalizes the tool arguments and retrieves candidate works from Open
   Library. Candidates are stored in a request-scoped search session and matched against
   the results from the LLM. This prevents hallucinated books.
4. Gemini compares all retrieved candidates with the original query, groups obvious
   duplicate editions or work records, assigns a ranking, and writes a short explanation
   based only on the returned metadata. Exact title or author evidence is favored over
   broad thematic similarity.
5. The API validates the model's structured response, resolves the opaque IDs back to the
   server-owned book records, removes scores at or below the match threshold, orders the
   remaining results, and returns at most 12 candidates to the UI.

Open Library calls use a separate cache and retry transient failures with exponential
backoff and jitter. Completed searches are also cached.
This dual cache allows for broader cache hits. A user that searches "Books about teddy bears"
will get a cache hit if they try again because of the controller cache.
The book provider cache allows similar queries to get cache hits when the LLM asks for books with
the same request shape from Open Library.

## Key assumptions

- The users is trying the loosly find a book. This means returning multiple candidates
  is preferable to a single answer. Also, related books are welcomed even if a primary book is identified.
- Queries are human-written text and may contain conflicting information
- Open Library is the source of truth for book metadata, but its records can be incomplete,
  duplicated, or inconsistent. 
- Open Library returns the authors object sorted by contribution importance.  An improvement 
  adding extra calls to verify authors would be needed before making stronger claims about authorship.

## Design decisions

- Gemini handles the genuinely ambiguous work: interpreting messy text, choosing search terms,
  comparing candidates, and writing grounded explanations. Input validation, Open Library access,
  caching, result filtering, and response shaping remain deterministic server responsibilities.
- Tool use is intentionally bounded to three Open Library searches per request, with up to six
  distinctive keywords and 25 candidates per search. This allows query refinement while placing a
  predictable ceiling on external calls and model context.
- Open Library records stay server-owned and are exposed to Gemini through opaque, request-scoped
  candidate IDs. The API ignores unknown IDs and validates scores, explanations, duplicates, result
  order, and result count so the model cannot introduce books that were never retrieved.
- Candidates with the same Open Library work key are de-duplicated deterministically within a
  request. Cross-record editions and duplicate works are grouped by Gemini because Open Library
  does not provide a consistently reliable canonical relationship in every search result.
- Only candidates scoring above 60 are returned, ordered by score, with a maximum of 12 results.
  This favors a short useful set while still preserving ambiguity when several matches are
  credible.
- Two caches serve different purposes: complete searches are cached for 60 minutes by exact user
  query, while normalized Open Library requests are cached for six hours. This reduces latency,
  Gemini usage, and repeated catalog traffic at the cost of accepting short-lived stale results.
- Transient Open Library failures are retried twice with exponential backoff and jitter. Failed
  Gemini or Open Library requests become `502` responses; there is no lower-quality deterministic
  search fallback in the current scope.

## Testing Strategy  

## Features completed

- 1 Accepts messy plain text
- 2 Ai is used for interpretation
- 3 Open Library is used for candidates
- 4 Results are ranked
- 5 There is a UI to work with the API
- LLM based ranking
- De-duplication accross editions
- Caching (both on API for users and Open Library for LLM calls)
- retries/resiliance with exponetial backoff and jitter
- AI transitions and animations
- Mobile responsive

## Improvements
- Improving the Author rankings instead of going of the assumption
- 