# Q5 — Green Architecture Framework (GAF)

---

## Slide 1: What is the Green Architecture Framework?

The Green Architecture Framework (GAF) is a set of principles for designing software systems that are **resource-efficient and environmentally sustainable**.

Its goal is to reduce the carbon footprint, energy consumption, and resource waste of software systems — without sacrificing functionality or reliability.

**Core principle:** Every unnecessary computation, every byte stored that is never read, and every idle resource is waste — and waste has an environmental cost.

---

## Slide 2: The Main Principles of GAF

**Measure**
Establish a baseline. You cannot reduce what you cannot see. Instrument systems to understand where resources are actually consumed.

**Reduce**
Eliminate unnecessary computation. Do less work per request. Avoid redundant processing.

**Reuse**
Cache results. Serve the same data without recomputing it. Share resources across consumers.

**Recycle**
Clean up what is no longer needed. Data that is never accessed but still stored consumes energy every day. Logs, backups, and stale records should have a defined lifetime.

**Responsible design**
Make architectural decisions with resource consumption as an explicit criterion — not just correctness and performance.

---

## Slide 3: Why GAF is Relevant for Large Systems

Individual software services may seem cheap to run. But large systems consist of:
- Dozens of microservices
- Hundreds of database connections
- Terabytes of logs
- Thousands of requests per second

**At scale, inefficiency compounds:**
- A query that hits the database instead of a cache — multiplied by 10,000 requests/minute — means 10,000 unnecessary DB reads per minute
- Logs that grow without bound eventually require more storage, more replication, more backup — all consuming energy
- Services that are never turned off when not needed idle and consume power continuously

**Cloud infrastructure directly translates resource usage into CO₂ emissions.**
Green Architecture is therefore not just an ethical concern — it is also an economic one. Efficient systems cost less to run.

---

## Slide 4: GAF in Happy Headlines — Reduce & Reuse (Caching)

**Redis caching reduces repeat database queries.**

In ArticleService, every `GET /articles/{continent}/{id}` first checks Redis:

```csharp
var raw = await db.StringGetAsync($"article-cache:{continent}:{id}");
if (raw.HasValue)
{
    _metrics.Hits.Inc();
    return Ok(JsonSerializer.Deserialize<Article>((string)raw!));
}
// Only hits PostgreSQL on a cache miss
```

**ArticleCachePreloader** (`src/ArticleService/Services/ArticleCachePreloader.cs`) warms the cache on service startup — meaning the first requests after a restart are also served from memory, not disk.

**CommentService** applies the same pattern for comments per article — Redis is checked before PostgreSQL on every read.

**Effect:** The majority of read traffic never reaches the database. Fewer queries = less compute = less energy.

---

## Slide 5: GAF in Happy Headlines — Recycle (Log Cleanup)

Logs are essential for observability — but logs that are older than a useful retention window serve no purpose and consume storage indefinitely.

The `log-cleanup` service in `docker/compose.yml` runs a cron job **every night at 02:00**:

```yaml
log-cleanup:
  image: alpine:latest
  entrypoint: |
    /bin/sh -c "
    apk add --no-cache dcron curl &&
    echo '0 2 * * * curl -X DELETE http://elasticsearch:9200/logs-$$(date -d \"-7 days\" +\%Y.\%m.\%d)' | crontab - &&
    crond -f
    "
```

**Logs older than 7 days are automatically deleted from Elasticsearch.**

This prevents unbounded growth of the log index — which would otherwise require ever-increasing storage, more replication overhead, and slower queries over time.

**Effect:** Storage consumption stays bounded. Elasticsearch performance stays consistent. The environmental cost of the observability stack does not grow without limit.

---

## Slide 6: GAF in Happy Headlines — Reduce (Circuit Breakers & Lean Images)

**Circuit breakers prevent wasted compute on failing dependencies.**

When ProfanityService is unavailable, CommentService's circuit breaker returns immediately rather than waiting for a timeout on every request. This avoids:
- Threads blocked waiting for a response that will never come
- Retry storms that amplify load on an already struggling service
- Cascading failures that consume resources across the entire system

**Multi-stage Docker builds produce lean runtime images.**

Every Dockerfile in the project uses a two-stage build:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# ... compile ...

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
COPY --from=build /app/publish .
```

The final image contains **only the runtime and the compiled application** — not the SDK, source code, or build tools. This reduces image size significantly, meaning:
- Less storage in the container registry
- Faster image pulls on deployment
- Smaller attack surface

---

## Slide 7: Summary

| GAF Principle | Implementation in Happy Headlines |
|---|---|
| **Measure** | Prometheus metrics + Grafana dashboards expose resource usage in real time |
| **Reduce** | Circuit breakers stop wasted calls; multi-stage Docker builds reduce image size |
| **Reuse** | Redis caching in ArticleService and CommentService; cache preloading on startup |
| **Recycle** | Automatic daily deletion of Elasticsearch log indices older than 7 days |
| **Responsible design** | Z-axis database sharding allows right-sized databases per continent rather than one enormous shared instance |

GAF is relevant for large systems because inefficiency is invisible at small scale but enormously costly — both financially and environmentally — at production scale. Every architectural decision is also an energy decision.
