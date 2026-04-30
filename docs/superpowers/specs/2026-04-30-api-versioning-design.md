# API Versioning Design

**Date:** 2026-04-30
**Branch:** `feature/api-versioning`

## Goal

Introduce explicit URL-segment API versioning to the Ingestion API so future breaking changes can ship side-by-side with the current contract, and OpenAPI documents are produced per version.

## Decisions

| Topic | Choice |
| --- | --- |
| Versioning scheme | URL segment (`/api/v1/...`) |
| Migration of existing route | Hard cutover — `/api/vendor-transactions` is removed |
| Initial version | `1.0` only |
| Unversioned requests | Return 404 (no implicit default version) |
| OpenAPI | One document per discovered version, exposed at `/openapi/v{version}.json` |

## Architecture

Use the `Asp.Versioning.Mvc` and `Asp.Versioning.Mvc.ApiExplorer` packages (maintained at `dotnet/aspnet-api-versioning`). They integrate with .NET 10's built-in `Microsoft.AspNetCore.OpenApi` via `IApiVersionDescriptionProvider`.

Configuration:

- Reader: `UrlSegmentApiVersionReader`
- `ReportApiVersions = true` (emits `api-supported-versions` response header)
- `AssumeDefaultVersionWhenUnspecified = false`
- `GroupNameFormat = "'v'VVV"` (yields `v1`, `v2`, ...)

## Components & Changes

### `src/IngestionApi.Api/IngestionApi.Api.csproj`
Add package references:
- `Asp.Versioning.Mvc`
- `Asp.Versioning.Mvc.ApiExplorer`

### `src/IngestionApi.Api/Program.cs`
- Register API versioning with `AddApiVersioning(...).AddApiExplorer(...)`.
- Replace the single `AddOpenApi()` call with one `AddOpenApi(groupName)` per discovered version (resolved from `IApiVersionDescriptionProvider`).
- Replace the single `MapOpenApi()` call with one `MapOpenApi("/openapi/{documentName}.json")` covering all groups, so each version is reachable at `/openapi/v1.json`, `/openapi/v2.json`, etc.

### `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs`
- Add `[ApiVersion("1.0")]`.
- Change `[Route("api/vendor-transactions")]` to `[Route("api/v{version:apiVersion}/vendor-transactions")]`.
- No other changes — action methods, signature filter, validation, and handlers are untouched.

## Out of Scope

- Adding a v2 endpoint or any v2 sample.
- Versioning conventions for future controllers (will be applied on a per-controller basis as they're added).
- Header- or query-based version readers.
- Deprecation tooling — there are no deprecated versions yet.

## Testing

- Existing unit tests are unaffected; no controllers are exercised there directly.
- Manual verification:
  - `POST /api/v1/vendor-transactions/T` succeeds (same behaviour as today).
  - `POST /api/vendor-transactions/T` returns 404.
  - `GET /openapi/v1.json` returns a v1-only OpenAPI document containing both `T` and `T+1` endpoints.
  - Response on a successful versioned call includes `api-supported-versions: 1.0`.

## Delivery

- All changes land on `feature/api-versioning`.
- Squash to a single commit before merging to `main`.
