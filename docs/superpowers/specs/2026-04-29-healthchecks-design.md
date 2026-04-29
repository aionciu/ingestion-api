# Health Checks Design

**Date:** 2026-04-29
**Branch:** `feature/healthchecks`

## Goal

Expose two HTTP health check endpoints for nginx to probe. Nginx uses liveness to know the process is up and readiness to decide whether to route traffic.

## Endpoints

| Endpoint | Purpose | Success | Failure |
|---|---|---|---|
| `GET /health/live` | Liveness — process is running | `200 Healthy` | n/a (always healthy) |
| `GET /health/ready` | Readiness — SQL Server reachable | `200 Healthy` | `503 Unhealthy` |

Both endpoints are unauthenticated and public.

## Approach

ASP.NET Core built-in Health Checks (`Microsoft.Extensions.Diagnostics.HealthChecks`) with the `AspNetCore.HealthChecks.SqlServer` community package for the SQL Server probe.

The SQL Server check executes `SELECT 1` against the existing `DefaultConnection` connection string to verify database connectivity.

## Changes

**`IngestionApi.Api.csproj`**
- Add NuGet package: `AspNetCore.HealthChecks.SqlServer`

**`Program.cs`**
- Register: `builder.Services.AddHealthChecks().AddSqlServer(connectionString)`
- Map liveness: `app.MapHealthChecks("/health/live", ...)` — tags filter to exclude the SQL check
- Map readiness: `app.MapHealthChecks("/health/ready", ...)` — runs all checks including SQL

No changes to Domain, Application, or Infrastructure layers.

## Branch

Feature branch `feature/healthchecks` created from `master`.
