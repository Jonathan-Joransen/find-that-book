# Architecture Tradeoffs

## Explicit Pipeline vs. Tool Calling

We’re choosing the explicit pipeline:

**LLM extracts search terms → Open Library search → LLM ranks results**

This approach gives us:

- Predictable cost and latency: exactly two LLM calls and a controlled number of Open Library requests.
- Easier testing: extraction, retrieval, and ranking can each be tested independently.
- Better observability: we can see which stage failed and inspect its inputs and outputs.
- Stronger control: the model cannot skip searches, repeat them unnecessarily, or exceed API limits.

With tool calling, the model requests an Open Library search, our application executes it, and the results return to the model. This enables adaptive behavior, such as reformulating a weak search or trying multiple queries. However, it introduces variable cost and latency, less predictable behavior, and more complicated testing and debugging. It also still requires at least two LLM requests, so it does not inherently reduce calls.

Tool calling would be worthwhile if evaluations show that iterative searches substantially improve difficult queries. For the current fixed workflow, explicit orchestration is simpler, more reliable, and easier to maintain.
