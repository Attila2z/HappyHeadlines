# Q1 — The C4 Model: Explained

## What is the C4 Model?

The C4 Model is a hierarchical framework for visualising software architecture at four levels of detail. Created by Simon Brown, it is named after the four diagram types it defines: **Context**, **Container**, **Component**, and **Code**.

The core idea is that no single diagram can serve every audience. A CEO and a backend developer need fundamentally different representations of the same system. C4 solves this by providing one coherent model with multiple entry points — each level is appropriate for a different audience and a different question.

---

## The four levels

| Level | Name | Question answered | Audience |
|-------|------|-------------------|----------|
| C1 | System Context | What does the system do and who uses it? | Everyone, including non-technical stakeholders |
| C2 | Container | What are the major building blocks and how do they communicate? | Technical and business stakeholders |
| C3 | Component | What is inside this container? | Developers and architects |
| C4 | Code | How is this component implemented? | Developers only |

Each level **zooms in on one element from the level above**. A C2 diagram expands one system from C1. A C3 diagram expands one container from C2. This nesting is what makes the model coherent — all levels describe the same system from different distances.

---

## The four levels in detail

### C1 — System Context
The highest level. Shows the system as a single box with its external actors and external systems. No implementation detail — no mention of microservices, databases, or technology. The purpose is to establish the boundary of the system and show who and what interacts with it.

This is the diagram you show to a product owner or a business executive. It should be readable by anyone.

### C2 — Container
Zooms into the system to show its deployable units: services, databases, message queues, frontends. Shows which technology each container uses and how they communicate (HTTP, AMQP, etc.). Does not show internal structure — just the major building blocks and the connections between them.

"Container" here means any deployable/runnable unit — not specifically a Docker container, though in modern systems those often coincide.

### C3 — Component
Zooms into one container to show its internal logical structure: controllers, services, repositories, clients. This is the diagram architects use when designing or explaining the internals of one service.

### C4 — Code
The lowest level — classes, interfaces, methods. Rarely maintained manually because IDEs can generate equivalent diagrams on demand (class diagrams, call graphs). C4 diagrams become outdated quickly as code changes.

---

## Why this matters — the communication problem

Without a shared model, technical and non-technical stakeholders talk past each other. A single architecture diagram tends to either:
- Be too abstract to be useful for developers, or
- Be too detailed to be understood by business stakeholders

C4 solves this with a single model that has **multiple entry points**. A business stakeholder engages with C1 — plain language, actors, and relationships. A technical lead engages with C2 — services, databases, technology choices. A developer engages with C3 — the internals of the specific service they are working on.

The levels are connected: C1 and C2 describe the same system. C2 and C3 describe the same containers. There is no inconsistency between them.

---

## C4 in Happy Headlines — C1: System Context

Defined in `docs/structurizr/workspace.dsl`.

**Actors (users of the system):**
- **Publisher** — drafts, reviews, and publishes articles
- **Reader** — reads articles, posts comments, subscribes to the newsletter

**External systems (outside the boundary of Happy Headlines):**
- **Email Delivery Provider** — sends newsletters via SMTP
- **Monitoring & Observability System** — receives logs and traces from all services

**The system itself:**
- **Happy Headlines** — the positive news website and newsletter platform

The C1 diagram shows nothing about microservices, databases, or queues. It only answers: what does the system do, and who interacts with it?

---

## C4 in Happy Headlines — C2: Container

The C2 diagram expands Happy Headlines into its deployable components.

**Services (10):**
WebApp, Website, ArticleService, DraftService, PublisherService, CommentService, ProfanityService, SubscriberService, NewsletterService, LogCleanupService

**Databases (5):**
- ArticleDatabase — Z-axis sharded, one PostgreSQL instance per continent (8 total)
- DraftDatabase, CommentDatabase, ProfanityDatabase, SubscriberDatabase

**Message Queues (2):**
- ArticleQueue — carries published articles from PublisherService to ArticleService
- SubscriberQueue — carries subscriber events

**Observability infrastructure:**
- Logstash, Elasticsearch, Kibana (ELK Stack) for centralised log aggregation

The C2 diagram is rendered from the Structurizr DSL in `docs/structurizr/workspace.dsl` — diagrams as code, version-controlled alongside the source. This means the architecture documentation stays in sync with the codebase through the same review and merge process as code changes.

---

## Structurizr DSL — diagrams as code

The architecture is defined in a `.dsl` file rather than drawn in a visual tool. Benefits:

- **Version-controlled** — changes to the architecture are tracked in git alongside code changes
- **Reviewable** — architecture changes go through the same pull request process as code
- **Consistent** — the diagram is generated from the DSL, so it cannot drift from the definition
- **Diffable** — you can see exactly what changed between two versions of the architecture

This is the same principle as infrastructure-as-code applied to architecture documentation.

---

## Summary

| Concept | Key point |
|---------|-----------|
| C4 purpose | Solves the "one diagram for everyone" anti-pattern |
| C1 | System boundary + actors — no technical detail |
| C2 | Deployable units and their communication — technology visible |
| C3 | Internal structure of one container |
| C4 | Code level — rarely maintained; IDEs generate on demand |
| Structurizr DSL | Diagrams as code — version-controlled, reviewable, consistent |

C1 communicates the product to stakeholders. C2 communicates the architecture to the team. C3 and C4 serve developers working on specific components.
