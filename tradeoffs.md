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
