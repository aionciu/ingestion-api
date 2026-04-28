# Ingestion API — Improvement Plan

Created: 2026-04-29
Scope: address findings from the performance / security / Clean Architecture / SOLID review.

Items are grouped by priority. Each task has: What, Where, Why, How, Acceptance.

---

## Task 1 — Stop leaking exception.Message to clients
- **Where:** `src/IngestionApi.Api/Middleware/ExceptionHandlingMiddleware.cs:38`
- **Why:** Internal/SQL/stack messages reach callers on 5xx responses.
- **How:** For 5xx responses return `{ "detail": "Internal error", "traceId": <CorrelationId from HttpContext.Items> }`. Known safe types (ValidationException, ArgumentException) may still include their messages.
- **Acceptance:** Unhandled exception returns generic body. CorrelationId is echoed in the body.

## Task 2 — Replace SQL-message string matching with SQL error codes
- **Where:** `src/IngestionApi.Infrastructure/Persistence/UnitOfWork.cs:22-37`
- **Why:** Grepping `InnerException.Message` for "UNIQUE"/"duplicate"/"constraint" is locale-dependent and misclassifies FK/CHECK violations.
- **How:** Cast `ex.GetBaseException()` to `Microsoft.Data.SqlClient.SqlException` and check `Number` (2601 or 2627 = unique violation). Throw `ConflictException` only for those; rethrow everything else.
- **Acceptance:** Unit test with a mocked `DbUpdateException` wrapping a `SqlException` with Number=2627 → `ConflictException`. A different Number → original exception bubbles.

## Task 3 — Sanitize X-Correlation-Id header
- **Where:** `src/IngestionApi.Api/Middleware/CorrelationIdMiddleware.cs:17-25`
- **Why:** Client-controlled value is reflected to the response and logs; CR/LF chars enable log injection.
- **How:** Accept the header only if it matches `^[A-Za-z0-9\-]{1,36}$` (or parses as a Guid). Otherwise generate a fresh `Guid.NewGuid().ToString()`.
- **Acceptance:** Header value `foo\r\nbar` is replaced. Valid GUID header is accepted as-is.

## Task 4 — Fix broken audit-log route path + remove raw body logging
- **Where:** `src/IngestionApi.Api/Middleware/RequestResponseLoggingMiddleware.cs:44-58`
- **Why:** Path check compares `/api/transactions` but actual route is `/api/vendor-transactions` — audit capture never runs. Also logging raw body leaks financial PII.
- **How:** Remove `CaptureRequestBody` entirely. Log only: method, path, status, elapsed ms. Structured audit fields (VendorId, OperationId) are already logged inside the command handlers.
- **Acceptance:** No body is logged. Middleware logs exactly: method, path, elapsed, status.

## Task 5 — Switch to DbContext pooling + retry + command timeout
- **Where:** `src/IngestionApi.Infrastructure/DependencyInjection.cs:19-20`
- **How:** Replace `AddDbContext` with `AddDbContextPool` and configure `EnableRetryOnFailure(3)` and `CommandTimeout(30)` on the SQL Server options.
- **Acceptance:** Build passes; existing integration tests still green.

## Task 6 — Use AddRange for bag inserts
- **Where:** `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionCommandHandler.cs:40-52`; `CreateTransactionT1CommandHandler.cs:43-76`
- **How:** Add `void AddRange(IEnumerable<TEntity> entities)` to `IRepository<TEntity,TId>` and implement in `GenericRepository`. Replace per-bag `Add` calls with a single `AddRange`.
- **Acceptance:** Handlers call `AddRange` once per request. `GenericRepository` delegates to `DbSet.AddRange`.

## Task 7 — Remove unbounded GetAllAsync
- **Where:** `src/IngestionApi.Domain/Common/IRepository.cs`; `src/IngestionApi.Infrastructure/Persistence/Repositories/GenericRepository.cs`
- **Why:** Full table scan with no pagination is a footgun.
- **How:** Remove `GetAllAsync` from the interface and from `GenericRepository`. Verify no callers.
- **Acceptance:** Interface has no `GetAllAsync`. Build passes.

## Task 8 — Move handler DI registrations into Application layer
- **Where:** `src/IngestionApi.Infrastructure/DependencyInjection.cs:28-29`; `src/IngestionApi.Application/DependencyInjection.cs`
- **Why:** Application owns the handler abstractions; Infrastructure should not reference Application command types.
- **How:** Move the two `AddScoped<ICommandHandler<...>, ...>` lines to `Application/DependencyInjection.cs`. Add any needed `using` directives. Infrastructure `DependencyInjection.cs` retains only DbContext, UoW, and repositories.
- **Acceptance:** Infrastructure no longer imports Application command namespaces. Build and tests pass.

## Task 9 — Move HTTP request DTOs out of the controller file
- **Where:** `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs:111-120`
- **How:** Create `src/IngestionApi.Api/Contracts/CreateTransactionRequest.cs` with `CreateTransactionRequest` and `BagItemDto` records. Remove them from the controller file. Add a `using` in the controller.
- **Acceptance:** Controller file contains only the controller class. New `Contracts/` file exists with the records.

## Task 10 — Replace magic TransactionType int with an enum
- **Where:** `src/IngestionApi.Domain/Entities/Transaction.cs:12`; both command handlers
- **How:** Add `public enum TransactionType { Declared = 1, Processed = 2, Difference = 3 }` in `Domain/Entities/`. Change `Transaction.TransactionType` property to `TransactionType`. Update handlers to use the enum values. Update EF configuration if needed (store as int).
- **Acceptance:** No magic numbers in handlers. EF migration not required (stored as int already).

---

## Suggested execution order
1, 2, 3, 4 (security/correctness)
5, 6, 7 (perf)
8, 9, 10 (architecture)
