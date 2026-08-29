# Architecture Tradeoffs

## Bounded Tool-Calling Book Finder

The search uses one `BookFinderPrompt` with access to a read-only `search_open_library`
tool. Gemini interprets the reader's evidence, chooses an Open Library query, evaluates
the returned candidates, and may reformulate the search when the first results are weak
or ambiguous.

The agent is deliberately bounded:

- It must search Open Library before returning results.
- It can make at most three sequential searches.
- Each tool call accepts a likely title, likely author, and up to six distinctive keywords.
- The application executes every Open Library request and retains the canonical book objects.
- Gemini returns only opaque candidate IDs, scores, and short reasons; it cannot rewrite book metadata.
- The server accepts only candidates observed in the current request, removes scores at or below 60,
  sorts by descending score, and returns at most 12 books.

This design has less predictable latency than a fixed pipeline because difficult searches can use
additional Open Library and model round trips. In exchange, it can recover from a plausible but wrong
title or author by inspecting weak results and trying a materially different query. The hard limits,
canonical server-side metadata, output validation, and read-only tool keep that flexibility controlled.

## Bounded Canonical Author Enrichment

Open Library search results can contain multiple names in `author_name` without enough
provenance to safely call every name a primary author. The provider therefore retains the
parallel `author_key` values and enriches only the first configured number of results from
their canonical `/works/{id}.json` records.

Canonical work author references without a role, or with an explicit author/writer role,
are treated as primary-author evidence. Other explicit roles remain structured contributor
evidence. If a canonical author key is absent from the search result, the provider resolves
its display name from the author endpoint. Repeated work and author lookups are cached for
the request, and failed enrichment falls back to search names marked as unverified.

This adds network latency, so enrichment is bounded by `WorkEnrichmentLimit` and runs only
for Open Library's highest-ranked search results. Open Library records can still be missing
or incorrect; canonical evidence is stronger provenance, not a guarantee of bibliographic
correctness.
