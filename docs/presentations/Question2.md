# Q2 — The AKF Scale Cube: Explained

## What is the AKF Scale Cube?

The AKF Scale Cube is a three-dimensional model for thinking about scalability. It was defined in the book *The Art of Scalability* by Martin Abbott and Michael Fisher (AKF Partners). The model provides a vocabulary for describing three fundamentally different approaches to scaling a system, each solving a different kind of problem.

The three axes are **X** (clone it), **Y** (split by function), and **Z** (split by data). They are not ranked — they are different tools for different scaling problems, and most production systems apply more than one.

---

## The three axes

### X-axis — Horizontal Duplication ("Clone it")
Run multiple identical copies of the same service behind a load balancer. Every instance can handle any request. The load balancer distributes traffic across instances.

- **What it scales:** throughput and availability
- **What it does not scale:** data volume; if the database is the bottleneck, adding more instances does not help
- **Prerequisite:** the service must be stateless — no in-process state that would make one instance's response different from another's

### Y-axis — Functional Decomposition ("Split by function")
Decompose a monolith into separate services, each responsible for a single capability. ArticleService handles articles, CommentService handles comments, SubscriberService handles subscriptions — no service does more than its defined function.

- **What it scales:** development velocity (teams can work independently), fault isolation (a failure in one service does not affect others), and independent deployability
- **What it does not scale:** data volume within a single service

### Z-axis — Data Partitioning ("Split by data")
Each instance handles a distinct subset of the data, partitioned by a key — user ID, region, continent. Requests are routed to the instance responsible for that data partition.

- **What it scales:** data volume; each instance holds only a fraction of the total data
- **Benefit:** indexes stay small, queries stay fast, data can be located closer to the users it serves
- **What it does not scale:** throughput if all data lands in the same partition (hot partition problem)

---

## X-axis trade-offs

**Benefits:**
- Simple — run more instances with no code changes required
- Provides redundancy and fault tolerance; if one instance fails, others continue serving traffic
- Scales throughput linearly — two instances handle roughly twice the traffic

**Challenges:**
- Shared or replicated state becomes a bottleneck — if each instance has its own in-memory cache, caches can diverge and requests see inconsistent data
- Sessions and local caches require a shared external layer (Redis) to stay consistent
- All instances must be updated together, requiring a rolling deployment strategy
- Does not help if the bottleneck is the database — all instances share the same DB

**When X-axis is not enough:** if one feature is 100× more popular than others, scaling all features equally with X-axis wastes resources. Y-axis decomposition allows the high-traffic service to be scaled independently.

---

## X-axis in Happy Headlines — ArticleService

Three identical ArticleService instances (`app1`, `app2`, `app3`) run behind an Nginx load balancer on port 80.

```yaml
# docker/compose.yml
app1:
  build:
    context: ../src/ArticleService
    dockerfile: Dockerfile
  restart: always

app2:
  build:
    context: ../src/ArticleService
    dockerfile: Dockerfile
  restart: always

app3:
  build:
    context: ../src/ArticleService
    dockerfile: Dockerfile
  restart: always

nginx:
  image: nginx:alpine
  ports:
    - "80:80"
  depends_on: [ app1, app2, app3 ]
```

Nginx uses round-robin distribution. Any request can go to any instance. This works because ArticleService is stateless — it holds no in-process state that would make one instance's response differ from another's.

---

## How statelessness is maintained

X-axis scaling requires that any instance can serve any request. This is achieved by externalising all state:

**Shared PostgreSQL databases (per continent)**
All three `app` instances connect to the same set of PostgreSQL databases. No instance holds article data locally. A query from `app1` and the same query from `app3` return identical results.

**Shared Redis cache (`redis-article`)**
All three instances read from and write to the same Redis instance. A cache entry written by `app1` is visible to `app2` and `app3`. Cache hits are consistent regardless of which instance serves the request.

```yaml
Redis__ConnectionString: "redis-article:6379"
```

**Nginx load balancer**
Routes traffic in round-robin. Because all state is external, it does not matter which instance receives a given request — they are all equivalent.

The key principle: **state lives outside the service**. The service itself is a stateless processing unit. This is what makes X-axis scaling straightforward.

---

## Z-axis in Happy Headlines — ArticleDatabase sharding

ArticleService applies Z-axis scaling to its database layer. Instead of one large shared PostgreSQL database, there are eight separate instances — one per continent — each storing only the articles for that region.

| Database | Partition Key |
|----------|---------------|
| `postgres-africa` | `Continent = "Africa"` |
| `postgres-asia` | `Continent = "Asia"` |
| `postgres-europe` | `Continent = "Europe"` |
| `postgres-northamerica` | `Continent = "NorthAmerica"` |
| `postgres-southamerica` | `Continent = "SouthAmerica"` |
| `postgres-oceania` | `Continent = "Oceania"` |
| `postgres-antarctica` | `Continent = "Antarctica"` |
| `postgres-global` | `Continent = "Global"` |

`DatabaseRouter.cs` in ArticleService inspects the `Continent` field on each request and routes it to the correct database. The routing logic is transparent to the rest of the service.

**Why this matters:** each database stores only a fraction of the total article volume. PostgreSQL indexes stay small, which keeps query performance fast regardless of how many articles are published globally. It also enables data locality — articles for European readers can be stored on infrastructure in Europe.

---

## Y-axis in Happy Headlines — the entire microservice architecture

The Y-axis decomposition is the entire architecture of Happy Headlines. Every capability is its own independently deployable service:

| Service | Capability |
|---------|-----------|
| ArticleService | Storing and serving articles |
| CommentService | Storing and moderating comments |
| ProfanityService | Filtering profanity from text |
| DraftService | Managing article drafts |
| PublisherService | Accepting and routing article submissions |
| SubscriberService | Managing newsletter subscriptions |
| NewsletterService | Composing and sending newsletters |

Each service has its own database, its own deployment lifecycle, and can be scaled independently. A spike in comment traffic does not require scaling ArticleService. A failure in ProfanityService does not bring down ArticleService.

---

## Summary

| Axis | What it scales | Applied in Happy Headlines |
|------|---------------|---------------------------|
| **X** | Throughput and availability | 3 ArticleService instances behind Nginx load balancer |
| **Y** | Development velocity and fault isolation | Entire architecture — each capability is its own microservice |
| **Z** | Data volume and locality | ArticleDatabase sharded by continent (8 PostgreSQL instances) |

X-axis scaling was straightforward because ArticleService was designed stateless from the start — all state lives in external databases and a shared Redis cache. Statelessness is a prerequisite for X-axis scaling, not an afterthought.
