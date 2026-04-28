# Correlation ID and Console Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add correlation ID and timestamp prefix to console logs, and configure console logging per environment (enabled in Development, disabled in Production/Staging).

**Architecture:** This is a configuration-only change. The CorrelationIdMiddleware already pushes correlation IDs to Serilog's LogContext, and RequestResponseLoggingMiddleware already logs requests. We simply update the OutputTemplate in appsettings.json and create environment-specific overrides to control where logs are written.

**Tech Stack:** Serilog configuration via appsettings.json, built-in .NET Core LogContext

---

### Task 1: Update appsettings.json with OutputTemplate

**Files:**
- Modify: `src/IngestionApi.Api/appsettings.json`

**Current state:** Console sink exists but has no OutputTemplate specified. Serilog uses a default template that doesn't include CorrelationId.

- [ ] **Step 1: Read the current appsettings.json**

```bash
cat src/IngestionApi.Api/appsettings.json
```

Expected output shows Serilog section with WriteTo array containing Console and File sinks.

- [ ] **Step 2: Update the console sink with OutputTemplate**

Replace this:
```json
{ "Name": "Console" },
```

With this:
```json
{
  "Name": "Console",
  "Args": {
    "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{CorrelationId}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
  }
},
```

The complete WriteTo section should look like:
```json
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
```

Verify the file is valid JSON when done.

- [ ] **Step 3: Verify the change**

Run:
```bash
dotnet build IngestionApi.slnx
```

Expected: Build succeeds (no JSON syntax errors).

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Api/appsettings.json
git commit -m "feat: add timestamp and correlation ID to console log output template"
```

---

### Task 2: Create appsettings.Development.json

**Files:**
- Create: `src/IngestionApi.Api/appsettings.Development.json`

**Purpose:** Override Serilog config for Development environment to ensure console logging is enabled and set appropriate minimum log level.

- [ ] **Step 1: Create the file**

Create `src/IngestionApi.Api/appsettings.Development.json` with these contents:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

This enables Debug level logging in Development, allowing more verbose output than the base Information level.

- [ ] **Step 2: Verify file was created**

Run:
```bash
ls -la src/IngestionApi.Api/appsettings.Development.json
```

Expected: File exists.

- [ ] **Step 3: Verify JSON validity**

Run:
```bash
dotnet build IngestionApi.slnx
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Api/appsettings.Development.json
git commit -m "feat: add Development-specific logging config with Debug level"
```

---

### Task 3: Create appsettings.Production.json

**Files:**
- Create: `src/IngestionApi.Api/appsettings.Production.json`

**Purpose:** Disable console logging in Production, keeping logs file-based only to avoid console overhead and keep production output clean.

- [ ] **Step 1: Create the file**

Create `src/IngestionApi.Api/appsettings.Production.json` with these contents:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
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

This overrides the base configuration to:
- Remove the console sink (file-based logging only)
- Set minimum level back to Information (standard production level)

- [ ] **Step 2: Verify file was created**

Run:
```bash
ls -la src/IngestionApi.Api/appsettings.Production.json
```

Expected: File exists.

- [ ] **Step 3: Verify JSON validity**

Run:
```bash
dotnet build IngestionApi.slnx
```

Expected: Build succeeds.

- [ ] **Step 4: Create appsettings.Staging.json (same as Production)**

Staging should also have no console logging. Create `src/IngestionApi.Api/appsettings.Staging.json` with the same contents as appsettings.Production.json:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
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

- [ ] **Step 5: Commit all environment configs**

```bash
git add src/IngestionApi.Api/appsettings.Production.json src/IngestionApi.Api/appsettings.Staging.json
git commit -m "feat: disable console logging in Production and Staging environments"
```

---

### Task 4: Verify Serilog Configuration

**Files:**
- Test: All appsettings files
- Manual verification via running the application

**Purpose:** Ensure that the configuration loads correctly and logs are formatted as expected.

- [ ] **Step 1: Build the solution**

Run:
```bash
dotnet build IngestionApi.slnx
```

Expected: Build succeeds with no warnings about configuration.

- [ ] **Step 2: Run tests to ensure nothing broke**

Run all integration tests:
```bash
dotnet test tests/IngestionApi.Tests.Integration/ -v
```

Expected: All tests pass, including `LoggingIntegrationTests.cs`:
- `HealthCheck_ReturnsOkAndLogsRequest` ✓
- `CorrelationIdPropagates_InRequestAndResponse` ✓
- `NewCorrelationIdGenerated_IfNotProvided` ✓

- [ ] **Step 3: Manual verification in Development**

Run the API in Development mode:
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/IngestionApi.Api
```

Expected: Application starts successfully and logs appear in console showing:
```
[2026-04-28 HH:MM:SS.fff] [GUID-HERE] [INF] Now listening on...
```

(Verify at least one log line shows the timestamp, correlation ID, and level prefix.)

- [ ] **Step 4: Send a test request**

In another terminal, send a request:
```bash
curl http://localhost:5000/health -i
```

(Adjust endpoint to an actual health check or API endpoint)

Expected: Console logs show request with correlation ID:
```
[2026-04-28 HH:MM:SS.fff] [GUID] [INF] GET /health started
[2026-04-28 HH:MM:SS.fff] [GUID] [INF] GET /health completed in XXms with status 200
```

Response headers include:
```
X-Correlation-Id: GUID
```

- [ ] **Step 5: Stop the application**

Press Ctrl+C to stop.

- [ ] **Step 6: Commit verification notes (optional)**

If creating a test report:
```bash
git log --oneline -5
```

Should show your recent commits in order.

---

### Task 5: Final Integration and Smoke Test

**Files:**
- Test: `tests/IngestionApi.Tests.Integration/Logging/LoggingIntegrationTests.cs`

**Purpose:** Run the full test suite to ensure all integration tests pass with the new configuration.

- [ ] **Step 1: Run all unit tests**

Run:
```bash
dotnet test tests/IngestionApi.Tests.Unit/ -v
```

Expected: All unit tests pass.

- [ ] **Step 2: Run all integration tests**

Run:
```bash
dotnet test tests/IngestionApi.Tests.Integration/ -v
```

Expected: All integration tests pass. The logging tests verify:
- Correlation ID is present in response headers
- Correlation ID propagates correctly when provided
- Correlation ID is auto-generated when not provided

- [ ] **Step 3: Run the full test suite**

Run:
```bash
dotnet test
```

Expected: All tests (unit + integration) pass.

- [ ] **Step 4: Verify log files are created correctly**

Check that log files were created during test run:
```bash
ls -la logs/
```

Expected: `ingestion-*.log` files exist (may be empty if tests don't log much).

- [ ] **Step 5: Final commit summary**

Review all changes:
```bash
git log --oneline -5
```

Expected output shows commits:
1. "feat: add timestamp and correlation ID to console log output template"
2. "feat: add Development-specific logging config with Debug level"
3. "feat: disable console logging in Production and Staging environments"

- [ ] **Step 6: Verify git status is clean**

Run:
```bash
git status
```

Expected: "On branch master" / "On branch main" with "nothing to commit, working tree clean".

---

## Summary

**What was implemented:**
- ✅ appsettings.json updated with OutputTemplate: `[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{CorrelationId}] [{Level:u3}] {Message:lj}{NewLine}{Exception}`
- ✅ appsettings.Development.json created with Debug level for verbose logging
- ✅ appsettings.Production.json and appsettings.Staging.json created with console logging disabled
- ✅ All integration tests pass
- ✅ Correlation ID and timestamp appear in all logs

**Configuration coverage:**
- Base config (appsettings.json): Template defined, console enabled
- Development: Debug level enabled, inherits console from base
- Production: Console disabled, file-based only
- Staging: Console disabled, file-based only

**No code changes required.** The existing CorrelationIdMiddleware and RequestResponseLoggingMiddleware infrastructure handles the rest.
