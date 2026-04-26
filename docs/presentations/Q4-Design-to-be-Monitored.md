# Q4 — Design to be Monitored

---

## Slide 1: What Does "Design to be Monitored" Mean?

Observability is not something you bolt on after a system is built — it must be **designed in from the start**.

"Design to be monitored" means every service is built with the explicit goal of being observable in production.

**Three pillars:**
- **Metrics** — quantitative measurements over time (counters, gauges, histograms)
- **Logging** — structured records of discrete events
- **Tracing** — following a single request as it travels across multiple services

Without all three, diagnosing a production issue is guesswork.

---

## Slide 2: What Should Be Captured

**Metrics** — answer *"How is the system performing right now?"*
- Request rate, error rate, latency (the RED method)
- Resource usage: CPU, memory, connections
- Business metrics: cache hit ratio, active subscribers, comments pending review

**Logging** — answer *"What happened and when?"*
- Structured JSON (machine-readable and human-readable)
- Every significant event: service start, DB migration, request received, error thrown
- Enriched with context: service name, machine name, request ID, correlation ID

**Tracing** — answer *"Which services did this request touch and how long did each take?"*
- A trace spans the entire lifetime of one user request across all services
- Each unit of work within a trace is a *span*
- Distributed tracing makes latency bottlenecks visible in a way that single-service logs cannot

---

## Slide 3: Why Y-axis Scaling Creates a Monitoring Problem

**Y-axis scaling** decomposes a monolith into microservices, each responsible for one function.

In a monolith, a single request stays in one process — one log file, one call stack.

After Y-axis decomposition, one user request may touch:
```
Website → ArticleService → ArticleDatabase
        ↘ CommentService → ProfanityService → ProfanityDatabase
```

**The problem:**
- Each service produces its own logs in isolation
- A failure in ProfanityService appears as an error in CommentService's logs with no obvious link
- Without a shared trace ID, correlating events across services requires manual detective work
- Performance bottlenecks are invisible without cross-service timing data

**Solution:** Centralised log aggregation + distributed tracing with a shared correlation ID injected into every log line and propagated across every service boundary.

---

## Slide 4: The Observability Stack in Happy Headlines

```
Services (all)
    │  Structured JSON logs (Serilog)
    ▼
Logstash  ──► Elasticsearch  ──► Kibana
    (ingest)      (store)         (visualise logs)

Services (all)
    │  Traces (OpenTelemetry)
    ▼
Zipkin
    (visualise request flow across services)

Services (all)
    │  Metrics (prometheus-net)
    ▼
Prometheus  ──► Grafana
    (scrape)        (dashboards)
```

All three pillars are implemented across every service from day one.

---

## Slide 5: Logging Across Services

Every service uses **Serilog** configured identically, enriched with a `service` property and shipped to Logstash over HTTP.

**PublisherService** (`src/PublisherService/Controllers/PublisherController.cs`) logs the full lifecycle of a publication request:

```csharp
_logger.LogInformation("Publication request received for '{title}' by {author}", request.Title, request.Author);
_logger.LogWarning("Publication rejected — ProfanityService unavailable for '{title}'", request.Title);
_logger.LogInformation("Article '{title}' approved and queued for continent {continent}", request.Title, request.Continent);
```

**ArticleService** logs every queue message consumed and every cache decision:

```csharp
_logger.LogInformation("Article '{title}' from queue saved to {continent} database", message.Title, message.Continent);
_logger.LogDebug("ArticleCache HIT for {continent}/{id}", continent, id);
```

In Kibana you can query: `service:"PublisherService" AND title:"Breaking News"` to trace a specific article from submission through moderation, queuing, and storage — across two services — using only log correlation.

---

## Slide 6: Metrics and Tracing

**Metrics — Prometheus + Grafana**

ArticleService exposes a custom cache hit ratio metric (`src/ArticleService/Services/ArticleCacheMetrics.cs`):

```csharp
public readonly Counter Hits   = Metrics.CreateCounter("article_cache_hits_total",   "Cache hits");
public readonly Counter Misses = Metrics.CreateCounter("article_cache_misses_total", "Cache misses");
```

Grafana displays this as a dashboard — visible in `docker/grafana/dashboards/cache-hit-ratio.json`.

All services also expose standard HTTP metrics via `app.UseMetricServer()` and `app.UseHttpMetrics()` — request rate, error rate, and latency out of the box.

**Tracing — OpenTelemetry + Zipkin**

Every service is configured with OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ArticleService"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddZipkinExporter(...));
```

When a request flows from the Website through ArticleService, Zipkin captures the full trace — showing exactly which span took how long.

---

## Slide 7: Summary

- "Design to be monitored" means observability is a first-class requirement, not an afterthought
- Y-axis scaling (microservices) makes monitoring *harder* — a single user action now spans multiple services — which is exactly why the three pillars (metrics, logging, tracing) are non-negotiable
- In Happy Headlines: **Serilog → ELK** for logs, **OpenTelemetry → Zipkin** for traces, **prometheus-net → Grafana** for metrics — all configured identically across every service from the first line of code
