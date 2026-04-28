# Correlation ID and Console Logging Enhancement

**Date:** 2026-04-28  
**Status:** Design Approved

## Overview

Enhance observability by including correlation IDs in log output and configuring console logging per environment. This allows developers and operators to easily trace requests through the system while keeping production logs clean and focused on file-based storage.

## Current State

- CorrelationIdMiddleware already exists and pushes correlation IDs to Serilog's LogContext
- Console logging is configured but output template doesn't display the correlation ID
- RequestResponseLoggingMiddleware logs requests/responses but they don't show correlation context
- Correlation ID header propagation is fully tested and working

## Design

### 1. Configuration Changes

#### appsettings.json (base configuration)

Update the Serilog console sink to include OutputTemplate with timestamp and correlation ID:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{CorrelationId}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/ingestion-.log",
          "rollingInterval": "Day"
        }
      }
    ],
    "Enrich": [ "FromLogContext" ]
  }
}
```

The template format:
- `{Timestamp:yyyy-MM-dd HH:mm:ss.fff}` — ISO 8601 timestamp with milliseconds
- `{CorrelationId}` — The correlation ID from LogContext (pushed by middleware)
- `{Level:u3}` — Three-letter log level (INF, WRN, ERR)
- `{Message:lj}` — Left-justified message
- `{Exception}` — Full exception details if present

#### appsettings.Development.json

Ensure console logging is enabled (explicitly):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

#### appsettings.Production.json

Disable console logging, log only to file:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/ingestion-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### 2. Code Changes

**No code changes required.** The infrastructure is already in place:

- CorrelationIdMiddleware (Program.cs line 20) extracts/generates correlation IDs and pushes to LogContext
- All ILogger instances automatically inherit the CorrelationId from the enriched context
- RequestResponseLoggingMiddleware logs are automatically enriched

### 3. Behavior

All logs written by any handler, middleware, or service will include:

```
[2026-04-28 14:35:22.123] [550e8400-e29b-41d4-a716-446655440000] [INF] POST /api/transactions started
[2026-04-28 14:35:22.145] [550e8400-e29b-41d4-a716-446655440000] [INF] Transaction payload received: {...}
[2026-04-28 14:35:22.167] [550e8400-e29b-41d4-a716-446655440000] [INF] POST /api/transactions completed in 44ms with status 201
```

### 4. Environment-Specific Behavior

| Environment | Console | File | Notes |
|-------------|---------|------|-------|
| Development | ✓ Enabled | ✓ Enabled | Full visibility for debugging |
| Staging | ✗ Disabled | ✓ Enabled | Minimal console overhead |
| Production | ✗ Disabled | ✓ Enabled | Clean logs, file-based only |

### 5. Testing & Verification

**Integration tests** (existing, no changes):
- `HealthCheck_ReturnsOkAndLogsRequest` — verifies correlation ID in response headers
- `CorrelationIdPropagates_InRequestAndResponse` — verifies correlation ID propagation
- `NewCorrelationIdGenerated_IfNotProvided` — verifies fallback GUID generation

**Manual verification**:
1. Run `dotnet run --project src/IngestionApi.Api` in Development
2. Send a request: `curl http://localhost:5000/api/health` (or actual endpoint)
3. Verify console output includes `[TIMESTAMP] [GUID] [LEVEL]` format
4. Verify file logs in `logs/` also capture the events

**No new tests required** — correlation ID behavior is already tested; we're only adding output formatting.

### 6. Out of Scope

- Structured JSON logging (not needed for current visibility goals)
- Custom LogContext enrichment beyond correlation ID
- Distributed tracing integration (can be added later)
- Correlation ID validation beyond length (already capped at 36 chars)

## Success Criteria

- [ ] Console logs display timestamps and correlation IDs when running in Development
- [ ] Console logging is disabled in Production/Staging environments
- [ ] File logs continue to work unchanged
- [ ] All existing integration tests pass
- [ ] Correlation ID flows through every logged message in a request

## Dependencies

- Serilog (already installed)
- .NET LogContext (built-in)
- Existing CorrelationIdMiddleware and RequestResponseLoggingMiddleware

## Risks & Mitigation

| Risk | Mitigation |
|------|-----------|
| OutputTemplate typo breaks logging | Use exact format from this spec; test immediately |
| Console logging degrades prod performance | Explicitly disabled in Production config |
| CorrelationId null in logs | Middleware generates GUID fallback; always present |

