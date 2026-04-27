# Q3 — Fault Isolation: Explained

## What is fault isolation?

Fault isolation is the practice of containing a failure within a defined boundary so it cannot spread to the rest of the system. The goal is not to prevent failures — failures are inevitable — but to ensure that when one service fails, it fails alone. Other services continue to operate normally.

The analogy is watertight compartments in a ship. If one compartment floods, the bulkheads stop the water. The rest of the ship stays afloat.

---

## The four core patterns

### 1. Bulkheads (Swimlanes)
Each service gets its own dedicated resource pool: its own threads, its own database connections, its own memory. No resources are shared between services. A spike or crash in one service cannot consume resources belonging to another.

### 2. Circuit Breaker
A circuit breaker wraps calls to a downstream service and tracks the failure rate. When failures exceed a threshold, the circuit "opens" — subsequent calls are short-circuited immediately without waiting for a timeout. After a recovery window, the circuit enters a half-open state and sends one test request. If it succeeds, the circuit closes again.

The three states:
- **Closed** — normal operation, calls go through
- **Open** — calls fail immediately without hitting the downstream service
- **Half-open** — one test request is sent; success closes the circuit, failure reopens it

### 3. Timeouts
Never wait indefinitely for a response. A hung downstream service must not block the caller's thread indefinitely. Without timeouts, a slow dependency will eventually exhaust all available threads in the calling service, causing it to fail as well. Timeouts are the simplest and first line of defence.

### 4. Fallback
Define, in code, what the system does when a dependency is unavailable. Examples: return cached data, return a default/degraded response, queue the request for later processing. A well-defined fallback is what converts a dependency failure into degraded-but-functional behaviour rather than a crash.

---

## Implementing isolation between two services — the checklist

| # | Requirement | Why |
|---|-------------|-----|
| 1 | Separate processes (containers) | Separate memory and CPU budgets |
| 2 | Separate databases | Failure in one DB does not affect the other |
| 3 | No shared in-process resources | No shared thread pools, connection pools, or caches |
| 4 | Timeouts on all outbound HTTP calls | Prevents indefinite blocking |
| 5 | Circuit breaker on each call site | Prevents cascading load on a failing service |
| 6 | Defined fallback behaviour | Caller knows exactly what to do when the callee is down |

**The diagnostic question:** "If Service B goes down completely right now, what does Service A do?" The answer must be defined in code, not left to chance.

---

## How it is implemented in Happy Headlines

### CommentService ↔ ProfanityService — swimlane design

These two services are fully isolated at the resource level:

| Resource | CommentService | ProfanityService |
|----------|----------------|------------------|
| Container | `commentservice` | `profanityservice` |
| Database | `postgres-comments` | `postgres-profanity` |
| Redis cache | `redis-comment` | — |

They communicate via a single synchronous HTTP call, and nothing else. A crash in ProfanityService has zero effect on CommentService's memory, threads, or database connections.

### The circuit breaker — `ProfanityClient.cs`

```csharp
public async Task<string?> FilterProfanity(string text)
{
    try
    {
        var response = await _http.PostAsJsonAsync("/profanity/filter", new { text });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FilterResult>();
        return result?.FilteredText;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "ProfanityService unavailable - comment will be PendingReview");
        return null; // null = open circuit signal
    }
}
```

The try/catch is the circuit. When ProfanityService is unavailable, the exception is caught, a warning is logged, and `null` is returned. No exception propagates to the caller. No thread is left waiting. `null` is the agreed signal that the circuit is open.

### The fallback — `CommentsController.cs`

```csharp
var filteredContent = await _profanityClient.FilterProfanity(request.Content);

var comment = new Comment
{
    Content = filteredContent ?? request.Content,
    Status  = filteredContent != null
              ? CommentStatus.Approved
              : CommentStatus.PendingReview  // fallback
};
```

When `filteredContent` is `null` (circuit open), the comment is saved with its original unfiltered content and given the status `PendingReview`. The user's comment is not lost. The request returns a success response. A human moderator reviews pending comments when ProfanityService recovers.

This is the fallback pattern: **reduced functionality, not a crash**.

---

## Summary

| Concept | Implementation |
|---------|---------------|
| Bulkhead | Separate containers, separate databases, no shared resources |
| Circuit breaker | `try/catch` returning `null` as the open circuit signal |
| Timeout | HTTP client timeout on outbound calls |
| Fallback | `PendingReview` status — comment saved, request succeeds |

Fault isolation means ProfanityService can be completely offline and users can still post comments. The system degrades gracefully.
