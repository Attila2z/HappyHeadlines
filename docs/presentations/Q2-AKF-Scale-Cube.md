# Q2 — The AKF Scale Cube

---

## Slide 1: What is the AKF Scale Cube?

A three-dimensional model for thinking about scalability, introduced by AKF Partners.
Each axis represents a different *type* of scaling — not a rank of better/worse, but different tools for different problems.

**The three axes:**
- **X-axis** — Horizontal duplication
- **Y-axis** — Functional decomposition
- **Z-axis** — Data partitioning

---

## Slide 2: The Three Dimensions

**X-axis — Clone it**
Run multiple identical copies of the same service behind a load balancer.
Scales *throughput*. Every instance handles any request.

**Y-axis — Split it by function**
Decompose a monolith into separate services, each responsible for one capability.
Scales *development velocity and fault isolation*. Each service can be deployed independently.

**Z-axis — Split it by data**
Each instance (or database) handles a distinct subset of the data, typically partitioned by a key (user ID, region, continent).
Scales *data volume and locality*.

---

## Slide 3: X-axis Trade-offs and Challenges

**Benefits:**
- Simple to implement — just run more instances of the same image
- No code changes required
- Works well for stateless services
- Provides redundancy and fault tolerance

**Challenges:**
- All instances must be kept in sync — shared or replicated state becomes a bottleneck
- Memory and compute are duplicated across every instance regardless of actual load patterns
- Sessions and local caches can become inconsistent — requires a shared cache layer (e.g. Redis)
- Deployment complexity increases — all instances must be updated together or with a rolling strategy
- Does not help if the bottleneck is in the database, not the service layer

**When X-axis is not enough:**
If one feature is 100× more popular than others, scaling all features equally wastes resources.
That is when Y-axis (split by function) becomes the better tool.

---

## Slide 4: ArticleService — X-axis Scaling in Practice

The ArticleService is horizontally duplicated into **three identical instances** behind an **Nginx load balancer**.

```
Client Request
      │
      ▼
   Nginx (port 80)
   ├── app1:8080
   ├── app2:8080
   └── app3:8080
```

Defined in `docker/compose.yml`:
```yaml
app1:
  build: { context: ../src/ArticleService, dockerfile: Dockerfile }
  restart: always

app2:
  build: { context: ../src/ArticleService, dockerfile: Dockerfile }
  restart: always

app3:
  build: { context: ../src/ArticleService, dockerfile: Dockerfile }
  restart: always

nginx:
  image: nginx:alpine
  ports:
    - "80:80"
  depends_on: [ app1, app2, app3 ]
```

---

## Slide 5: How Statelessness is Maintained

X-axis scaling requires that any instance can serve any request.
This is achieved by externalising all state:

**Shared PostgreSQL databases** (per continent) — all three instances connect to the same databases. No instance holds data locally.

**Shared Redis cache** (`redis-article`) — all three instances read from and write to the same cache. Cache hits are consistent regardless of which instance serves the request.

```yaml
Redis__ConnectionString: "redis-article:6379"
```

**Result:** Nginx can route any request to any of the three instances with no loss of correctness.

---

## Slide 6: Z-axis Scaling — ArticleDatabase

In addition to X-axis scaling on the *service*, a **Z-axis split** is applied to the *database*.

Eight separate PostgreSQL instances, one per continent plus a global database:

| Database | Partition Key |
|---|---|
| `postgres-africa` | `Continent = "Africa"` |
| `postgres-asia` | `Continent = "Asia"` |
| `postgres-europe` | `Continent = "Europe"` |
| `postgres-northamerica` | `Continent = "NorthAmerica"` |
| ... | ... |
| `postgres-global` | `Continent = "Global"` |

The `DatabaseRouter` class in ArticleService (`src/ArticleService/Services/DatabaseRouter.cs`) routes each request to the correct database based on the `continent` field.

**Benefit:** Each database only stores a fraction of the total data — indexes stay small, queries stay fast, and data can be located closer to its region.

---

## Slide 7: Summary

| Axis | Applied in Happy Headlines |
|---|---|
| X-axis | 3 ArticleService instances behind Nginx |
| Y-axis | Entire architecture — each capability is its own microservice |
| Z-axis | ArticleDatabase sharded by continent (8 databases) |

X-axis scaling was straightforward to apply because ArticleService was designed stateless from the start — all state lives in external databases and a shared Redis cache.
