# Q5 — Green Architecture Framework: Explained

## What is the Green Architecture Framework?

The Green Architecture Framework (GAF) is an approach to software design that treats resource efficiency and environmental sustainability as first-class architectural concerns — alongside correctness and performance. The core argument is that every architectural decision has a resource cost, and resource cost translates directly into energy consumption and CO₂ emissions.

The framework does not require a complete redesign. Many patterns that make a system faster and cheaper also make it greener — caching, circuit breakers, efficient container images — because they reduce unnecessary computation.

---

## Why it matters at scale

At small scale, inefficiency is invisible. A single unnecessary database query costs microseconds. But:

> A query that hits the database instead of the cache, multiplied by 10,000 requests per minute, means **10,000 unnecessary DB reads per minute** — each consuming CPU, network I/O, and disk I/O.

Large systems involve dozens of microservices, hundreds of database connections, terabytes of logs, and thousands of requests per second. At that scale, every inefficiency compounds. Cloud infrastructure translates resource usage directly into electricity consumption and CO₂ emissions. Efficient systems are therefore both cheaper and greener — the two goals are aligned.

---

## The five principles

### Measure
**You cannot reduce what you cannot see.**

Establish a baseline before claiming any improvement. Instrument the system to understand where resources are actually being consumed. Without measurement, optimisation is guesswork and you cannot verify that changes are having the intended effect.

### Reduce
**Eliminate unnecessary computation. Do less work per request.**

Every CPU cycle spent on work that produces no value is waste. This includes redundant processing, unnecessary database queries (when a cache could serve the result), blocking threads that are waiting for responses that will never come, and oversized images that take longer to pull and store.

### Reuse
**Serve the same data without recomputing it. Share resources across consumers.**

If the result of a computation can be stored and returned on subsequent requests, it should be. Caching is the primary mechanism. A cache hit means the database is not queried, the network is not hit, and CPU is not spent — for every subsequent request until the cache entry expires.

### Recycle
**Clean up what is no longer needed.**

Data stored but never accessed still occupies disk space and still consumes energy to maintain. Logs, backups, and stale records must have a defined lifetime. Without explicit cleanup policies, storage grows without bound, system performance degrades, and the operational cost of the system increases indefinitely.

### Responsible Design
**Make architectural decisions with resource consumption as an explicit criterion.**

Every design choice — database architecture, service decomposition, communication patterns, deployment strategy — has a resource cost. That cost should be evaluated alongside correctness and performance, not treated as a secondary concern.

---

## How each principle is implemented in Happy Headlines

### Measure — Prometheus + Grafana

Every service exposes a `/metrics` endpoint using `prometheus-net`. Prometheus scrapes all services every 15 seconds. Grafana renders dashboards.

Standard HTTP metrics (request rate, error rate, latency) are collected automatically via `app.UseHttpMetrics()`. ArticleService also exposes a custom cache hit ratio metric:

```csharp
// src/ArticleService/Services/ArticleCacheMetrics.cs
public readonly Counter Hits   = Metrics.CreateCounter("article_cache_hits_total",   "Cache hits");
public readonly Counter Misses = Metrics.CreateCounter("article_cache_misses_total", "Cache misses");
```

This gives real-time visibility into how effectively the cache is working — which is the evidence base for the Reuse principle.

---

### Reduce — Circuit Breakers and Lean Docker Images

**Circuit breakers** prevent wasted computation when a downstream service is unavailable. Without a circuit breaker, calls to an unavailable service block threads while waiting for timeouts. Those blocked threads hold memory and OS resources. If enough pile up simultaneously, the caller degrades — triggering retries, which amplify load further (a retry storm). The circuit breaker short-circuits immediately: no wait, no retry storm, no wasted resources.

**Multi-stage Docker builds** reduce image size at build time:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# ... compile ...
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
COPY --from=build /app/publish .
```
The final image contains only the ASP.NET runtime and the compiled application binary. No SDK, no source code, no build tools. Smaller images mean less storage in the container registry, less bandwidth consumed on every deployment, and faster pull times.

---

### Reuse — Redis Caching in ArticleService and CommentService

Both ArticleService and CommentService check Redis before querying PostgreSQL on every read:

```csharp
// src/ArticleService/Services/ArticleCache.cs
var raw = await db.StringGetAsync($"article-cache:{continent}:{id}");
if (raw.HasValue)
{
    _metrics.Hits.Inc();
    return Ok(JsonSerializer.Deserialize<Article>((string)raw!));
}
// Only reaches PostgreSQL on a cache miss
```

Cache key format: `article-cache:{continent}:{id}`. A cache hit returns the result from memory with no database query, no network round-trip to PostgreSQL, and no disk I/O. The majority of read traffic never reaches the database.

**Cache preloading** eliminates the cold-start problem. `ArticleCachePreloader` runs as a background service on startup and warms the cache with all existing articles before the first request is served. Even the first request after a restart is served from Redis.

---

### Recycle — Automatic Log Cleanup

Logs are essential for observability, but logs older than the operational retention window serve no purpose. Without cleanup, Elasticsearch indices grow indefinitely — consuming storage, slowing down queries (which costs more CPU per search), and increasing the energy cost of the observability stack over time.

The `log-cleanup` service in `docker/compose.yml` addresses this:

```yaml
log-cleanup:
  image: alpine:latest
  entrypoint: |
    /bin/sh -c "
    apk add --no-cache dcron curl &&
    echo '0 2 * * * curl -X DELETE \
      http://elasticsearch:9200/logs-$$(date -d \"-7 days\" +\%Y.\%m.\%d)' \
    | crontab - &&
    crond -f
    "
```

Every night at 02:00, it deletes the Elasticsearch index for logs that are exactly seven days old. Storage stays bounded. Elasticsearch query performance stays consistent. The environmental cost of the observability stack does not grow without limit.

---

### Responsible Design — Z-axis Database Sharding

ArticleService uses Z-axis sharding: eight separate PostgreSQL databases, one per continent. Each database is sized for the actual article volume of its region, rather than one enormous shared instance that must handle the combined write load from all eight continents simultaneously.

Benefits:
- Each database is right-sized — no over-provisioning
- Write contention is eliminated (each region writes to its own database)
- A database failure in one continent does not affect others
- Backup and maintenance operations can be performed per-continent

This is responsible design: the data architecture decision was made with resource consumption, isolation, and operational cost as explicit criteria.

---

## Summary table

| Principle | Implementation in Happy Headlines |
|-----------|-----------------------------------|
| **Measure** | Prometheus metrics + Grafana dashboards across all services; custom cache hit ratio counter |
| **Reduce** | Circuit breakers stop wasted calls; multi-stage Docker builds shrink image size |
| **Reuse** | Redis caching in ArticleService and CommentService; cache preloading on startup |
| **Recycle** | `log-cleanup` service deletes Elasticsearch indices older than 7 days, nightly at 02:00 |
| **Responsible Design** | Z-axis database sharding: 8 right-sized PostgreSQL instances instead of one enormous shared one |

GAF is not a separate concern from performance or reliability. Many of the same patterns — caching, circuit breakers, efficient builds — serve all three goals simultaneously. The framework makes resource consumption an explicit design criterion rather than an afterthought.
