# Q4 — Design to be Monitored: Explained

## What does "design to be monitored" mean?

It means observability is treated as a first-class requirement — built in from the start, not added after the system is already running in production. A system that is designed to be monitored exposes the data needed to understand its behaviour, diagnose problems, and verify that it is working correctly.

The opposite — a system that was not designed for observability — forces engineers to guess when something goes wrong. In a microservices architecture, guessing is not acceptable.

---

## The three pillars of observability

### Metrics — *How is the system performing right now?*
Quantitative measurements collected over time: counters, gauges, histograms. Common examples:
- Request rate, error rate, latency (the RED method)
- CPU usage, memory usage
- Application-specific: cache hit ratio, queue depth, active subscribers

Metrics tell you **something is wrong**.

### Logging — *What happened and when?*
Structured records of discrete events. Good logs are:
- **Structured** (JSON, not plain text) so they can be queried programmatically
- **Enriched** with context: service name, machine name, request ID, correlation ID
- **Captured at every significant event**: service start, request received, error thrown, state changed

Logs tell you **what went wrong**.

### Tracing — *Which services did this request touch, and how long did each take?*
A trace follows a single user request across all the services it touches. Each unit of work within a trace is a **span**. Distributed tracing makes latency bottlenecks visible across service boundaries in a way that per-service logs cannot.

Traces tell you **where the problem is**.

> Without all three pillars, diagnosing a production issue is guesswork.

---

## Why Y-axis scaling makes monitoring harder

**In a monolith:** a single request stays in one process. One log file. One call stack. You open the log and read from top to bottom.

**After Y-axis decomposition (microservices):** one user request may touch many services:
```
Website → ArticleService → ArticleDB
        ↘ CommentService → ProfanityService → ProfanityDB
```
Each service writes its own isolated logs. A failure in ProfanityService appears in CommentService's logs as an error, with no automatic link back to the cause. Without a shared identifier (a trace ID or a structured field like article title), correlating what happened across four separate log streams requires manual detective work.

**The solution:** centralised log aggregation + distributed tracing with a correlation identifier injected into every log line and propagated across every service boundary.

---

## The observability stack in Happy Headlines

All three pillars are implemented across every service from the first commit.

```
Logging:   Services (Serilog) → Logstash → Elasticsearch → Kibana
Tracing:   Services (OpenTelemetry) → Zipkin
Metrics:   Services (prometheus-net) → Prometheus → Grafana
```

| Tool | Role |
|------|------|
| **Serilog** | Structured JSON logging library used by every service |
| **Logstash** | Ingests log events and forwards to Elasticsearch |
| **Elasticsearch** | Stores and indexes log events for querying |
| **Kibana** | Query interface for searching and filtering logs |
| **OpenTelemetry** | Instrumentation library that generates trace spans |
| **Zipkin** | Distributed trace visualisation UI |
| **prometheus-net** | .NET library that exposes a `/metrics` endpoint |
| **Prometheus** | Scrapes `/metrics` from all services every 15 seconds |
| **Grafana** | Renders metric dashboards from Prometheus data |

---

## Logging in practice — Serilog

Every service uses Serilog configured identically, enriched with a `service` property, and shipped to Logstash over HTTP.

**PublisherService** logs the full publication lifecycle:
```csharp
_logger.LogInformation("Publication request received for '{title}' by {author}", request.Title, request.Author);
_logger.LogWarning("Publication rejected - ProfanityService unavailable for '{title}'", request.Title);
_logger.LogInformation("Article '{title}' approved and queued for continent {continent}", request.Title, request.Continent);
```

**ArticleService** logs queue consumption and cache behaviour:
```csharp
_logger.LogInformation("Article '{title}' saved to {continent} database", message.Title, message.Continent);
_logger.LogDebug("ArticleCache HIT for {continent}/{id}", continent, id);
```

In Kibana, the query `service:"PublisherService" AND title:"Breaking News"` returns all log lines referencing that article from PublisherService. Because ArticleService logs the same title field, extending the query across both services correlates the full lifecycle — submission, moderation, queuing, and storage — using structured fields alone.

---

## Cross-service monitoring across the RabbitMQ queue

The publication flow crosses an async boundary — PublisherService publishes a message to RabbitMQ, and ArticleService consumes it. This breaks the distributed trace chain.

### What Zipkin sees (synchronous HTTP leg only)
```
PublisherService [0ms]
  └─ POST /profanity/filter [5ms → 48ms]
     ProfanityService
  └─ BasicPublish → article.publish [49ms]

Trace ends here — RabbitMQ is async.
```
Zipkin captures the HTTP leg automatically: the ProfanityService call timing, success/failure, and the moment the message is handed to the queue. It cannot cross the queue boundary because RabbitMQ has no built-in OpenTelemetry trace propagation.

### What Kibana sees (full lifecycle via structured fields)
```
Query: title:"Copenhagen Scientists Discover..."

[PublisherService]  Publication request received
[PublisherService]  Article approved and queued  continent: Europe
[ArticleService]    Article saved to Europe database
```
Three log lines. Two services. One article. Correlated by article title as a structured field — not by a trace ID.

**The two tools are complementary.** Zipkin covers the synchronous performance (timing, errors, latency). Kibana covers the full end-to-end lifecycle including the async queue leg. Together they give complete visibility.

---

## Metrics and tracing — implementation

### Custom cache metric (ArticleService)
```csharp
// src/ArticleService/Services/ArticleCacheMetrics.cs
public readonly Counter Hits   = Metrics.CreateCounter("article_cache_hits_total",   "Cache hits");
public readonly Counter Misses = Metrics.CreateCounter("article_cache_misses_total", "Cache misses");
```
Grafana renders this as a cache hit ratio dashboard (`docker/grafana/dashboards/cache-hit-ratio.json`).

### Standard HTTP metrics (all services)
```csharp
app.UseMetricServer(); // exposes /metrics endpoint for Prometheus to scrape
app.UseHttpMetrics();  // auto-tracks request rate, error rate, latency per endpoint
```
Every service gets RED metrics for free — no additional code required.

### OpenTelemetry configuration (all services)
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ArticleService"))
        .AddAspNetCoreInstrumentation()   // traces incoming HTTP requests
        .AddHttpClientInstrumentation()   // traces outgoing HTTP calls
        .AddZipkinExporter(...));
```
Every inbound and outbound HTTP call is automatically captured as a span. No manual instrumentation is required.

---

## Summary

| Pillar | Tool | What it answers |
|--------|------|----------------|
| Metrics | prometheus-net → Prometheus → Grafana | How is the system performing right now? |
| Logging | Serilog → Logstash → Elasticsearch → Kibana | What happened and when? |
| Tracing | OpenTelemetry → Zipkin | Which services did this request touch? |

Key insight: the RabbitMQ queue breaks the trace chain. Zipkin covers the synchronous HTTP leg; Kibana structured-field queries cover the async leg. Neither alone is sufficient — both together provide complete visibility.
