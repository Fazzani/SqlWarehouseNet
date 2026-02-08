---
description: Review code for security, performance, and correctness
mode: agent
tools:
    [
        "semantic_search",
        "read_file",
        "grep_search",
        "list_code_usages",
        "get_errors",
    ]
---

# Code Review

Perform a thorough code review of the recent changes.

## Checklist

### Security

- [ ] No token/secret exposure in logs or output
- [ ] SQL identifiers validated with `SafeIdentifierRegex`
- [ ] User input sanitized before use in API calls
- [ ] `HttpClient` headers don't leak auth tokens

### Correctness

- [ ] `CancellationToken` propagated through all async paths
- [ ] `IDisposable` resources properly disposed (`using`/`await using`)
- [ ] Error handling with user-friendly messages
- [ ] Edge cases handled (empty results, null values, network failures)

### Performance

- [ ] No unnecessary allocations in hot paths
- [ ] `Channel<T>` used for streaming (not buffering everything in memory)
- [ ] Parallel downloads for Arrow chunks
- [ ] JSON source generation used (no reflection)

### Style

- [ ] `sealed` classes where appropriate
- [ ] Nullable reference types properly annotated
- [ ] XML doc comments on public APIs
- [ ] Consistent naming conventions
