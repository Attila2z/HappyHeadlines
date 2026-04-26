# Q3 — Fault Isolation

---

## Slide 1: What is Fault Isolation?

Fault isolation is the architectural practice of **containing failures within a boundary** so they cannot cascade into other parts of the system.

The analogy: watertight compartments in a ship. If one compartment floods, the others remain intact — the ship stays afloat.

**Why it matters:**
- In a distributed system, any service can fail at any time (network, memory, third-party outage)
- Without isolation, one failing dependency can bring down the entire system
- With isolation, failures are localised — the rest of the system degrades gracefully

---

## Slide 2: Core Fault Isolation Patterns

**Bulkheads (Swimlanes)**
Separate resource pools per service — dedicated threads, connections, and memory.
A spike or failure in one swimlane does not starve another.

**Circuit Breaker**
Tracks failure rate to a downstream service.
When failures exceed a threshold, the circuit *opens*: calls are short-circuited immediately without waiting for a timeout.
After a recovery window, the circuit is tested again (*half-open* state).

**Timeouts**
Never wait indefinitely for a response. A hung dependency should not hang the caller.

**Fallback**
Define explicit behaviour for when a dependency is unavailable.
Examples: return cached data, return a degraded response, queue for later processing.

---

## Slide 3: Implementing Fault Isolation Between Two Services

To properly isolate two services:

1. **Separate processes** — each service runs in its own container with its own memory and CPU budget
2. **Separate databases** — no shared data stores; failures in one DB do not affect the other
3. **No shared in-process resources** — no shared thread pools, connection pools, or caches between the two
4. **Timeouts on all outbound HTTP calls** — the calling service does not block indefinitely
5. **Circuit breaker on the call site** — wraps the HTTP call; opens on repeated failures
6. **Defined fallback behaviour** — the calling service must know exactly what to do when the callee is unavailable

The key question: *"If Service B goes down completely right now, what does Service A do?"*
The answer must be defined in code — not left to chance.

---

## Slide 4: CommentService ↔ ProfanityService — Swimlane Design

The two services live in completely separate swimlanes:

| Resource | CommentService | ProfanityService |
|---|---|---|
| Container | `commentservice` | `profanityservice` |
| Database | `postgres-comments` | `postgres-profanity` |
| Network | Docker bridge | Docker bridge |
| Redis cache | `redis-comment` | — |

No shared resources. A crash in ProfanityService has zero effect on CommentService's memory, threads, or database connections.

Communication is **synchronous HTTP only** — and that call is wrapped with fault isolation logic.

---

## Slide 5: The Circuit Breaker — ProfanityClient

`src/CommentService/Services/ProfanityClient.cs` wraps the HTTP call to ProfanityService:

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
        _logger.LogWarning(ex, "ProfanityService unavailable — comment will be marked PendingReview");
        return null;  // null = circuit open, caller handles fallback
    }
}
```

Returning `null` is the **open circuit signal** — the caller decides what to do.

---

## Slide 6: Fallback Behaviour — CommentService

`src/CommentService/Controllers/CommentsController.cs` implements the fallback:

```csharp
var filteredContent = await _profanityClient.FilterProfanity(request.Content);

var comment = new Comment
{
    Content = filteredContent ?? request.Content,
    Status  = filteredContent != null
        ? CommentStatus.Approved
        : CommentStatus.PendingReview   // fallback
};
```

**When ProfanityService is available:** Comment is filtered and saved as `Approved`.

**When ProfanityService is down:** Comment is saved as `PendingReview` with the original content. The request succeeds — the user's comment is not lost.

To test: `docker stop profanityservice` → post a comment → status is `PendingReview`.
Restart → future comments return to `Approved`.

---

## Slide 7: Summary

- Fault isolation prevents a single service failure from cascading into a full system outage
- The three tools used: **bulkheads** (separate containers + databases), **circuit breaker** (try/catch returning null), **fallback** (PendingReview status)
- CommentService continues to accept and store comments even when its dependency is completely unavailable
- The system degrades *gracefully* — reduced functionality, not a crash
