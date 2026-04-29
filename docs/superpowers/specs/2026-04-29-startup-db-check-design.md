# Startup Database Connectivity Check Design

**Date:** 2026-04-29
**Branch:** `feature/healthchecks`

## Goal

Refuse to start the application if the SQL Server database is unreachable, failing fast with a clear logged error and non-zero exit code.

## Approach

A hosted service (`IHostedService`) runs a connectivity check during application startup. If the check fails, it throws — causing ASP.NET Core to abort startup, emit a structured log entry via Serilog, and exit with a non-zero code.

## Components

**New file: `src/IngestionApi.Api/Startup/DatabaseStartupCheck.cs`**
- Implements `IHostedService`
- Injects `IServiceScopeFactory` (required because `AppDbContext` is scoped)
- In `StartAsync`: creates a scope, resolves `AppDbContext`, calls `CanConnectAsync()`
- If `false` or throws: re-throws `InvalidOperationException("Cannot connect to the database.")`
- `StopAsync`: no-op

**Modified: `src/IngestionApi.Api/Program.cs`**
- Add `builder.Services.AddHostedService<DatabaseStartupCheck>()`

No changes to Domain, Application, or Infrastructure layers.

## Failure Behavior

On startup with an unreachable database:
- Serilog logs the exception at Fatal/Error level
- Process exits with non-zero code
- nginx (or the container orchestrator) sees the failed start and does not route traffic

## Testing

Integration test in `tests/IngestionApi.Tests.Integration/Startup/DatabaseStartupCheckTests.cs`:
- Uses `WebApplicationFactory` with an invalid connection string
- Asserts that host startup throws (i.e., `CreateClient()` throws an exception)
