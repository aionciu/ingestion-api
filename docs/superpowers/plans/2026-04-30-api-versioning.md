# API Versioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- []`) syntax for tracking.

**Goal:** Add URL-segment API versioning (`/api/v1/...`) to the Ingestion API with one OpenAPI document per discovered version.

**Architecture:** Wire up the `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` packages in `Program.cs`, drive OpenAPI document registration from `IApiVersionDescriptionProvider`, and tag the existing `VendorTransactionsController` with `[ApiVersion("1.0")]` while moving its route under `api/v{version:apiVersion}/...`. Hard cutover — the old unversioned route is removed (returns 404).

**Tech Stack:** .NET 10, ASP.NET Core, `Asp.Versioning.Mvc` 8.x, `Asp.Versioning.Mvc.ApiExplorer` 8.x, `Microsoft.AspNetCore.OpenApi` 10.x.

**Spec:** `docs/superpowers/specs/2026-04-30-api-versioning-design.md`

**Branch / commit policy:** All work lands on `feature/api-versioning`. Per user instruction, **do not commit between tasks** — make a single commit at the very end (Task 5).

---

## File Map

| File | Action | Responsibility |
| --- | --- | --- |
| `src/IngestionApi.Api/IngestionApi.Api.csproj` | Modify | Add versioning package references |
| `src/IngestionApi.Api/Program.cs` | Modify | Configure API versioning + per-version OpenAPI documents |
| `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs` | Modify | Add `[ApiVersion("1.0")]`; route under `api/v{version:apiVersion}/...` |
| `docs/superpowers/specs/2026-04-30-api-versioning-design.md` | Already created | Design spec (untouched here) |

No new tests are added — there are no controller-level tests in this repo today, and the spec lists verification as manual (build + curl).

---

## Task 1: Add NuGet package references

**Files:**
- Modify: `src/IngestionApi.Api/IngestionApi.Api.csproj`

- [ ] **Step 1: Add package references**

In the existing `<ItemGroup>` containing `<PackageReference>` entries, add the two versioning packages. The full ItemGroup should become:

```xml
<ItemGroup>
  <PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
  <PackageReference Include="Asp.Versioning.Mvc" Version="8.1.0" />
  <PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" Version="8.1.0" />
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.6" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
  <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
  <PackageReference Include="Serilog.Sinks.Console" Version="6.1.1" />
  <PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
</ItemGroup>
```

- [ ] **Step 2: Restore packages**

Run: `dotnet restore IngestionApi.slnx`
Expected: completes without errors; both `Asp.Versioning.Mvc` and `Asp.Versioning.Mvc.ApiExplorer` resolve.

If 8.1.0 is unavailable, pick the latest 8.x available on nuget.org and use the same version for both packages.

---

## Task 2: Configure API versioning + per-version OpenAPI in `Program.cs`

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs`

- [ ] **Step 1: Add `using` directives**

At the top of `Program.cs`, add:

```csharp
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
```

- [ ] **Step 2: Replace the single `AddOpenApi()` call with versioning + per-version OpenAPI registration**

Find this block (around line 16-24):

```csharp
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
```

Replace with:

```csharp
builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// Register one OpenAPI document per discovered API version.
using (var sp = builder.Services.BuildServiceProvider())
{
    var provider = sp.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var description in provider.ApiVersionDescriptions)
    {
        builder.Services.AddOpenApi(description.GroupName);
    }
}

builder.Services.AddApplication();
```

Notes:
- `AddMvc()` (the `Asp.Versioning.Mvc` extension on the versioning builder) is required so controller-based routing picks up `[ApiVersion]`.
- The temporary `BuildServiceProvider()` is necessary because `AddOpenApi` needs a document name *at registration time*, and the version descriptions are discovered from MVC ApiExplorer. This is run once at startup; it's not a per-request cost.

- [ ] **Step 3: Replace `MapOpenApi()` with a templated mapping**

Find:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

Replace with:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}
```

This serves every registered document at `/openapi/<groupName>.json` (e.g. `/openapi/v1.json`).

- [ ] **Step 4: Build the API project**

Run: `dotnet build src/IngestionApi.Api/IngestionApi.Api.csproj`
Expected: 0 errors, 0 warnings (or only pre-existing warnings).

---

## Task 3: Version the `VendorTransactionsController`

**Files:**
- Modify: `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs`

- [ ] **Step 1: Add the `Asp.Versioning` using directive**

At the top of the file, alongside the other usings, add:

```csharp
using Asp.Versioning;
```

- [ ] **Step 2: Add `[ApiVersion]` and update the route**

Find the attributes on the class (lines 12-14):

```csharp
[ServiceFilter(typeof(VendorSignatureFilter))]
[ApiController]
[Route("api/vendor-transactions")]
public class VendorTransactionsController : ControllerBase
```

Replace with:

```csharp
[ServiceFilter(typeof(VendorSignatureFilter))]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vendor-transactions")]
public class VendorTransactionsController : ControllerBase
```

- [ ] **Step 3: Build the solution**

Run: `dotnet build IngestionApi.slnx`
Expected: 0 errors. Pre-existing warnings (if any) unchanged.

---

## Task 4: Manual verification

**Files:** none (smoke test)

- [ ] **Step 1: Run the API**

Run: `dotnet run --project src/IngestionApi.Api`
Wait for `Now listening on:` line. Note the HTTPS port (e.g. `https://localhost:7XXX`).

- [ ] **Step 2: Verify the OpenAPI document is served at `/openapi/v1.json`**

In a second terminal:

```bash
curl -k https://localhost:<port>/openapi/v1.json
```

Expected:
- HTTP 200
- JSON body whose `paths` keys include `/api/v1/vendor-transactions/T` and `/api/v1/vendor-transactions/T+1`.
- No path under `/api/vendor-transactions/...` (unversioned) appears.

- [ ] **Step 3: Verify the unversioned route returns 404**

```bash
curl -k -i -X POST https://localhost:<port>/api/vendor-transactions/T \
  -H "Content-Type: application/json" -d '{}'
```

Expected: HTTP 404.

- [ ] **Step 4: Verify the versioned route is reachable**

```bash
curl -k -i -X POST https://localhost:<port>/api/v1/vendor-transactions/T \
  -H "Content-Type: application/json" -d '{}'
```

Expected: HTTP **400** (missing `X-Vendor-Id` header) or **401** (signature filter rejects the request) — either response confirms the route reached the controller. A 404 here is a failure.

The response should also contain header `api-supported-versions: 1.0`.

- [ ] **Step 5: Stop the running API**

Ctrl+C in the terminal running `dotnet run`.

If any verification step fails, fix the cause before proceeding to Task 5.

---

## Task 5: Commit

**Files:** all modified files from Tasks 1-3.

- [ ] **Step 1: Review the diff**

Run: `git status` and `git diff`.
Expected files changed:
- `src/IngestionApi.Api/IngestionApi.Api.csproj`
- `src/IngestionApi.Api/Program.cs`
- `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs`
- `docs/superpowers/specs/2026-04-30-api-versioning-design.md` (untracked, created in brainstorming)
- `docs/superpowers/plans/2026-04-30-api-versioning.md` (this file, untracked)

- [ ] **Step 2: Stage the changes**

```bash
git add src/IngestionApi.Api/IngestionApi.Api.csproj \
        src/IngestionApi.Api/Program.cs \
        src/IngestionApi.Api/Controllers/VendorTransactionsController.cs \
        docs/superpowers/specs/2026-04-30-api-versioning-design.md \
        docs/superpowers/plans/2026-04-30-api-versioning.md
```

- [ ] **Step 3: Create the single commit**

```bash
git commit -m "$(cat <<'EOF'
feat(api): introduce URL-segment API versioning (v1)

Add Asp.Versioning.Mvc + ApiExplorer, route VendorTransactionsController
under /api/v{version:apiVersion}/..., and register one OpenAPI document
per discovered version (served at /openapi/v{version}.json in Development).

Hard cutover: the prior unversioned route /api/vendor-transactions is
removed and now returns 404.
EOF
)"
```

- [ ] **Step 4: Verify the commit**

Run: `git log -1 --stat`
Expected: one commit on `feature/api-versioning` touching the five files above.
