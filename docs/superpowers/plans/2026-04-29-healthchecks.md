# Health Checks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose `/health/live` and `/health/ready` endpoints for nginx probing, with the readiness endpoint verifying SQL Server connectivity.

**Architecture:** ASP.NET Core built-in health checks (`Microsoft.Extensions.Diagnostics.HealthChecks`) with the `AspNetCore.HealthChecks.SqlServer` package. Two `MapHealthChecks` registrations in `Program.cs` — liveness excludes the SQL check via tag filtering, readiness runs everything.

**Tech Stack:** .NET 10 / ASP.NET Core, `AspNetCore.HealthChecks.SqlServer` 9.0.0

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/IngestionApi.Api/IngestionApi.Api.csproj` | Modify | Add `AspNetCore.HealthChecks.SqlServer` package |
| `src/IngestionApi.Api/Program.cs` | Modify | Register and map health checks |
| `tests/IngestionApi.Tests.Unit/HealthChecks/HealthCheckEndpointTests.cs` | Create | Integration-style tests for both endpoints |

---

## Task 1: Create feature branch

- [ ] **Step 1: Create and switch to feature branch**

```bash
git checkout -b feature/healthchecks
```

Expected output:
```
Switched to a new branch 'feature/healthchecks'
```

---

## Task 2: Add NuGet package

**Files:**
- Modify: `src/IngestionApi.Api/IngestionApi.Api.csproj`

- [ ] **Step 1: Add the package**

```bash
dotnet add src/IngestionApi.Api/IngestionApi.Api.csproj package AspNetCore.HealthChecks.SqlServer --version 9.0.0
```

Expected output ends with:
```
PackageReference for package 'AspNetCore.HealthChecks.SqlServer' version '9.0.0' added to file '...'
```

- [ ] **Step 2: Verify build still passes**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/IngestionApi.Api.csproj
git commit -m "chore: add AspNetCore.HealthChecks.SqlServer package"
```

---

## Task 3: Register and map health checks in Program.cs

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs`

- [ ] **Step 1: Add health check registrations**

Replace the current `Program.cs` content with:

```csharp
using IngestionApi.Api.Middleware;
using IngestionApi.Application;
using IngestionApi.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
```

- [ ] **Step 2: Build to verify no compile errors**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Program.cs
git commit -m "feat: add /health/live and /health/ready endpoints"
```

---

## Task 4: Write and run tests

**Files:**
- Create: `tests/IngestionApi.Tests.Unit/HealthChecks/HealthCheckEndpointTests.cs`

> These are integration tests using `WebApplicationFactory`. They verify the HTTP contract without needing a real SQL Server — the readiness test asserts `503` when the DB is unreachable (which it will be in CI with no connection string).

- [ ] **Step 1: Check what test project exists and what packages it has**

```bash
ls tests/
```

Then read the test project's `.csproj` to see if `Microsoft.AspNetCore.Mvc.Testing` is already referenced.

- [ ] **Step 2: Add `Microsoft.AspNetCore.Mvc.Testing` if not present**

```bash
dotnet add tests/IngestionApi.Tests.Unit/IngestionApi.Tests.Unit.csproj package Microsoft.AspNetCore.Mvc.Testing --version 10.0.6
```

Skip this step if the package is already in the csproj.

- [ ] **Step 3: Write the failing tests**

Create `tests/IngestionApi.Tests.Unit/HealthChecks/HealthCheckEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace IngestionApi.Tests.Unit.HealthChecks;

public class HealthCheckEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LiveEndpoint_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsDegradedOrUnhealthyWhenDbUnreachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        // Without a real DB, the SQL check will fail → 503
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail for the right reason**

```bash
dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~HealthCheckEndpointTests" -v
```

Expected: `LiveEndpoint_ReturnsOk` fails because `Program` is not public yet (or passes if already public). `ReadyEndpoint` fails or passes depending on DB availability. If both fail with "could not find type Program", proceed to Step 5.

- [ ] **Step 5: Make Program accessible to the test project**

Add to `src/IngestionApi.Api/Program.cs` at the very bottom (after `app.Run();`):

```csharp
public partial class Program { }
```

Rebuild:

```bash
dotnet build IngestionApi.slnx
```

- [ ] **Step 6: Run tests again**

```bash
dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~HealthCheckEndpointTests" -v
```

Expected:
- `LiveEndpoint_ReturnsOk` → **PASS** (liveness never checks DB)
- `ReadyEndpoint_ReturnsDegradedOrUnhealthyWhenDbUnreachable` → **PASS** (no DB in test environment → 503)

If `ReadyEndpoint` returns `200` (DB is reachable in your environment), that is also correct — adjust the assertion accordingly.

- [ ] **Step 7: Commit**

```bash
git add tests/IngestionApi.Tests.Unit/HealthChecks/HealthCheckEndpointTests.cs src/IngestionApi.Api/Program.cs
git commit -m "test: add health check endpoint tests"
```

---

## Task 5: Smoke test manually

- [ ] **Step 1: Run the API**

```bash
dotnet run --project src/IngestionApi.Api
```

- [ ] **Step 2: Hit the liveness endpoint**

```bash
curl -i http://localhost:5000/health/live
```

Expected:
```
HTTP/1.1 200 OK
Content-Type: text/plain

Healthy
```

- [ ] **Step 3: Hit the readiness endpoint**

```bash
curl -i http://localhost:5000/health/ready
```

Expected: `200 Healthy` if SQL Server is running, `503 Unhealthy` if not.

- [ ] **Step 4: Stop the API** (`Ctrl+C`)
