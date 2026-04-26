# Q1 — The C4 Model

---

## Slide 1: What is the C4 Model?

C4 is a hierarchical framework for visualising software architecture at four levels of detail.
Created by Simon Brown — named after its four levels: **Context, Container, Component, Code**.

**Core idea:** Different audiences need different levels of detail.
A single diagram cannot serve both a CEO and a backend developer.

---

## Slide 2: The Four Levels

| Level | What it shows | Primary audience |
|---|---|---|
| **C1 — System Context** | The system and its external relationships | Everyone, including non-technical stakeholders |
| **C2 — Container** | Deployable units (services, databases, queues) and how they communicate | Technical and business stakeholders |
| **C3 — Component** | The internal structure of a single container | Developers and architects |
| **C4 — Code** | Classes, interfaces, implementation detail | Developers only |

Each level zooms in on one element from the level above.

---

## Slide 3: Primary Purpose of Each Level

- **C1 (Context):** Answers *"What does the system do and who uses it?"*
  Sets the boundary of the system. No technical jargon — purely about actors and relationships.

- **C2 (Container):** Answers *"What are the major building blocks and how do they talk?"*
  Shows technology choices: microservices, databases, queues, front-ends.

- **C3 (Component):** Answers *"What is inside this container?"*
  Shows controllers, services, repositories — the logical structure inside one deployable unit.

- **C4 (Code):** Answers *"How is this component implemented?"*
  Rarely maintained in practice — IDEs can generate this on demand.

---

## Slide 4: Bridging the Communication Gap

Without a shared model, technical and non-technical stakeholders talk past each other.

**The C4 solution:**
- C1 uses plain language — a business stakeholder can walk into a meeting, look at the diagram, and immediately understand what the system does and who it serves.
- C2 introduces technology but avoids implementation detail — a product manager can understand *that* there is a database without needing to know it runs PostgreSQL 16.
- C3 and C4 are developer-facing — they are never shown to non-technical audiences.

**Result:** One coherent model, multiple entry points. Each audience sees the level that is relevant to them without being overwhelmed by detail that does not apply to their role.

---

## Slide 5: C4 in Happy Headlines — C1 (System Context)

Defined in `docs/structurizr/workspace.dsl`.

**Actors:**
- `Publisher` — drafts, reviews, and publishes articles
- `Reader` — reads articles, posts comments, subscribes to the newsletter

**External systems:**
- `Email Delivery Provider` — used to send newsletters via SMTP
- `Monitoring & Observability System` — receives logs and traces

**The system:**
- `Happy Headlines` — the positive news website and newsletter platform

C1 shows *nothing* about microservices, databases, or queues — just the boundary of the system and who interacts with it.

---

## Slide 6: C4 in Happy Headlines — C2 (Container)

The C2 diagram zooms into the Happy Headlines system and reveals all deployable units:

**Services:** WebApp, Website, ArticleService, DraftService, PublisherService, CommentService, ProfanityService, SubscriberService, NewsletterService, LogCleanupService

**Databases:** ArticleDatabase (z-axis sharded per continent), DraftDatabase, CommentDatabase, ProfanityDatabase, SubscriberDatabase

**Queues:** ArticleQueue, SubscriberQueue

**Observability:** Logstash, Elasticsearch, Kibana

**Key relationships shown:**
- `Website → ArticleService` — fetch articles (HTTPS/REST)
- `SubscriberService → SubscriberQueue` — async event on new subscription
- `All services → Logstash` — structured log shipping

Rendered using the **Structurizr DSL** tool.

---

## Slide 7: Summary

- The C4 model solves the "one diagram for everyone" anti-pattern.
- Four levels of abstraction map to four distinct audiences.
- In Happy Headlines, C1 communicates the product to stakeholders; C2 communicates the architecture to the team.
- The Structurizr DSL keeps diagrams **as code** — version-controlled alongside the source.
