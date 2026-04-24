# FMEA — CI/CD Pipeline for HappyHeadlines

---

## Scoring Guide

| Scale | Severity (S) | Occurrence (O) | Detection (D) |
|---|---|---|---|
| 1–2 | Negligible — cosmetic or invisible to users | Almost never (< once a year) | Near-certain detection before any impact |
| 3–4 | Minor — slight degradation, workaround available | Occasional (a few times a year) | High likelihood of detection |
| 5–6 | Moderate — feature partially unavailable | Moderately frequent (monthly) | Moderate chance of detection |
| 7–8 | High — service unavailable or data incorrect | Frequent (weekly) | Low chance of detection |
| 9–10 | Critical — data loss, security breach, or full outage | Almost always | Cannot be detected before impact |

**RPN = Severity × Occurrence × Detection**  
Actions are prioritised by highest RPN.

---

## Failure Mode and Effects Analysis

| # | Process / Component | Failure Mode | Potential Effect | S | Potential Cause | O | Current Prevention | D | RPN | Recommended Action |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Pipeline Trigger | Push to `main` does not trigger the pipeline | New code never built or deployed; team assumes deployment happened | 7 | Branch filter misconfigured; GitHub webhook failure | 2 | GitHub built-in webhook | 5 | **70** | Add `workflow_dispatch` manual trigger; send status notification on completion |
| 2 | Build — NuGet Restore | Package restore fails | Pipeline fails; no deployment possible | 6 | NuGet registry temporarily unavailable; yanked package version | 3 | None | 2 | **36** | Cache NuGet packages with `actions/cache`; pin exact package versions in `.csproj` |
| 3 | Build — Compile & Test | Code compiles but contains runtime bugs | Broken code deployed to end users | 8 | No automated tests exist to catch logic errors | 5 | Manual code review | 8 | **320** | Add unit and integration test step; block merge to `main` without a green pipeline |
| 4 | Docker Build — Image | Image build fails | Pipeline fails; stale image remains in registry | 6 | Dockerfile syntax error; transient base-image layer unavailable | 2 | Multi-stage Dockerfile isolates build from runtime | 1 | **12** | Pin base image versions; enable automatic retries on transient failures |
| 5 | Docker Build — Base Image | Base image (`mcr.microsoft.com/dotnet`) unavailable | Cannot produce new images; all deployments blocked | 7 | Microsoft registry outage; deprecated image tag | 2 | None | 3 | **42** | Pin base image to a specific digest (`@sha256:…`); document a manual fallback procedure |
| 6 | Docker Push | Push to registry fails | New image not published; deployment would use stale image | 7 | Auth token expired; GHCR outage; network timeout | 2 | `GITHUB_TOKEN` is rotated automatically per run | 2 | **28** | Add retry logic on push step; alert (email/Slack) on push failure |
| 7 | Deployment — Startup | Container fails to start after image update | Service completely unavailable | 9 | Missing environment variable; port conflict; misconfigured `appsettings` | 3 | Docker `restart: always` policy retries | 5 | **135** | Add HTTP health-check smoke test step after deployment; fail pipeline if health check fails |
| 8 | Deployment — DB Migration | EF Core migration fails on new container start | Service instance crashes on boot; database left in inconsistent state | 9 | Breaking schema change (non-backward-compatible); database unreachable at startup | 3 | Manual review of migration files | 4 | **108** | Run migrations in a dedicated pre-deploy pipeline job; validate against a staging environment |
| 9 | Deployment — Partial Update | Some services updated to new version, others remain on old | Inter-service API incompatibility; potential data corruption | 8 | One Docker build or push fails while others succeed | 3 | None | 7 | **168** | Use `fail-fast: true` in matrix strategy; gate deployment on all images being pushed successfully |
| 10 | Post-Deployment — Health Check | No automated health verification after deploy | Silent failure; users experience errors before team is alerted | 8 | Health-check step not implemented in pipeline | 5 | Manual monitoring via Grafana | 7 | **280** | Add a pipeline step that calls each service's `/health` endpoint; fail pipeline if any returns non-2xx |
| 11 | Post-Deployment — Rollback | No automated rollback mechanism | Extended downtime after a bad deployment; manual recovery required | 8 | Rollback procedure not defined or automated | 4 | None | 6 | **192** | Tag images with commit SHA; document rollback command (`docker compose pull <old-sha>`); automate via pipeline |
| 12 | Security — Secret Exposure | Credentials or connection strings committed to the repository | Credential exposure; potential security breach; data exfiltration | 10 | Developer accidentally commits `.env`, `appsettings.json`, or inline secret | 2 | `.gitignore` for `.env` files | 7 | **140** | Enable GitHub secret scanning; add pre-commit hook (`gitleaks`); require PR review before merging |
| 13 | Pipeline Infrastructure | GitHub Actions runner unavailable | All pipeline executions fail until runner is restored | 5 | GitHub Actions platform outage | 2 | GitHub platform SLA | 2 | **20** | Monitor pipeline; consider a self-hosted runner as fallback for critical deployments |
| 14 | Pipeline Config — Workflow YAML | Workflow file contains a misconfiguration or syntax error | Pipeline silently skips steps or runs with incorrect parameters | 7 | YAML indentation error; incorrect condition expression | 3 | GitHub Actions syntax check on commit | 3 | **63** | Test workflow changes on a feature branch before merging; use `act` for local validation |

---

## Prioritised Action Summary

| Priority | # | RPN | Root Risk | Action |
|---|---|---|---|---|
| 1 | 3 | 320 | No automated tests | Add `dotnet test` step; block deployments without passing tests |
| 2 | 10 | 280 | No post-deploy health check | Add smoke-test step calling each service's `/health` endpoint |
| 3 | 11 | 192 | No rollback mechanism | Tag images with SHA; document and automate rollback |
| 4 | 9 | 168 | Partial deployment | `fail-fast: true` on matrix; gate deploy on all pushes succeeding |
| 5 | 12 | 140 | Secret committed to repo | Enable secret scanning; add `gitleaks` pre-commit hook |
| 6 | 7 | 135 | Container fails to start | Health-check smoke test after deploy; fail pipeline on non-healthy |
| 7 | 8 | 108 | DB migration fails at startup | Dedicated migration job; staging validation |

---

## Notes

- **RPN > 100** items are considered high risk and require action before the CI/CD pipeline is used in production.
- The highest-RPN item (FM3, missing tests, RPN 320) is architectural — the pipeline can be implemented now but should be treated as incomplete until a test suite is added.
- Items FM7 and FM10 are mitigated together by a single health-check step after deployment.
