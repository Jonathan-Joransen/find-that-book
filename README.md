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

- The biggest desgin desicsion is having the AI handle the Open Library request via a tool call
  - I am a big proponent of taking as many decisions away from AI workflows as possible to increase 
    determinism, but this use case flourishes with non-determinism. 
  - The call to Open Library might need to be made several times depending on what keywords the AI determines
    and on what results come back from the API. This is a perfect situation to use a tool call and allow the AI 
    to flourish in it's strengths of making sense of messy inputs.
  - Since this is a tool call we are keeping as much as we can deterministic.
    - It can only call that endpoint
    - It has retry logic and cached values
    - It is limited to 3 calls to prevent AI from going down a rabbit hole.

- There are other minor decisions. The first point is the biggest design decision by a large margin.
  here are some quick others.
  - Adapter pattern on providers so we can swap them out (new LLMs or new Book API can be dropped in)
  - Caching requests to our API and requests to open library to decrease repeated call lookups
  - 

## Testing Strategy  

Primarly want to test the results. 
- Are certain messy inputs getting the expected data.
- Are certain inputs not returning likely false positives

I wanted to use the tests to verify the LLM + Open Library was giving us high quality usable results.

There are some other unit tests, but I did not spend much time on those. 
I was mainly interested in the tests that proved our results would be high quality.

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
- Improving the determinism in Author relevance rankings 
- Add frontend tests
- Add e2e tests
- Add a deterministic fallback where we break queries into keywords and search open library when the LLM is down.
- Pull the grouping logic out of the AI to make it deterministic.
- shorten provder timeouts, specifically book providers should be lower.
- Add rate limiting
- Increase test coverange on backend
- Hardening against prompt injections from user or from book provider
  - Our exposure is limited by structured outputs and read only data, but moving external messages into a clear tagged section
    and maybe even scanning for LLM directions could help reduce potential attacks.
