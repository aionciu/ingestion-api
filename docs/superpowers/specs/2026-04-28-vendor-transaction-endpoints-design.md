# Vendor Transaction Endpoints Design (T and T+1)

**Date:** 2026-04-28  
**Feature:** Dummy controller for receiving vendor transactions with separate endpoints for T (today) and T+1 (tomorrow) with different validation rules.

---

## Overview

Create a `VendorTransactionsController` with two endpoints that accept vendor transaction data. Both endpoints accept the same request body structure but enforce different validation rules based on transaction timing (T vs T+1). The controller integrates with the existing CQRS/Handlers pattern.

---

## Requirements

- Two distinct HTTP endpoints for T and T+1 transactions
- Same request body structure with different validations per endpoint
- VendorId extracted from HTTP header (`X-Vendor-Id`)
- Multiple bags per transaction
- Database-level unique constraint to prevent duplicates
- HTTP status codes: 202 (success), 409 (conflict), 400 (validation failure)

---

## Request/Response Contract

### Request Headers
```
X-Vendor-Id: <string>
```

### Request Body (both endpoints)
```json
{
  "transactionId": "string",
  "bagId": "string",
  "bags": [
    {
      "operationId": "string",
      "declaredValue": "decimal",
      "processedValue": "decimal",
      "differenceValue": "decimal",
      "currency": "string"
    }
  ]
}
```

### Response

**202 Accepted** (success):
```json
{}
```

**409 Conflict** (duplicate):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "A transaction with these bag details already exists."
}
```

**400 Bad Request** (validation failure):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "bags": [
      "DeclaredValue must be greater than 0"
    ]
  }
}
```

---

## Validation Rules

### T Endpoint (`POST /api/vendor-transactions/T`)
**Per-bag rules:**
- `DeclaredValue` > 0
- `ProcessedValue` = 0
- `DifferenceValue` = 0

### T+1 Endpoint (`POST /api/vendor-transactions/T+1`)
**Per-bag rules:**
- `DeclaredValue` > 0
- `ProcessedValue` ≥ 0
- `DifferenceValue` ≥ 0

---

## Architecture & Design

### Domain Layer

**Transaction Entity** (`Domain/Entities/Transaction.cs`):
- Primary key: `Id` (Guid)
- `VendorId` (string) — from header, stored in DB
- `TransactionId` (string) — request identifier
- `BagId` (string) — root-level bag identifier
- `Bags` (ICollection<Bag>) — value objects

**Bag Value Object** (`Domain/ValueObjects/Bag.cs`):
- `OperationId` (string)
- `DeclaredValue` (decimal)
- `ProcessedValue` (decimal)
- `DifferenceValue` (decimal)
- `Currency` (string)

### Application Layer

**Commands:**
- `CreateTransactionTCommand` — request model for T transactions
- `CreateTransactionT1Command` — request model for T+1 transactions

**Validators:**
- `CreateTransactionTValidator` — FluentValidation validator for T rules
- `CreateTransactionT1Validator` — FluentValidation validator for T+1 rules

**Handlers:**
- `CreateTransactionTCommandHandler` — processes T transactions
- `CreateTransactionT1CommandHandler` — processes T+1 transactions

Both handlers:
1. Receive the command with VendorId (extracted from header by controller)
2. Validate via FluentValidation (automatic, before handler execution)
3. Persist via `IUnitOfWork.SaveChangesAsync()`
4. Catch `DbUpdateException` (from duplicate constraint violation) → return `Result.Conflict()`
5. Return `Result.Success()` on success

### Infrastructure Layer

**Entity Configuration** (`Persistence/Configurations/TransactionConfiguration.cs`):
- Configures `Transaction` and `Bag` via `IEntityTypeConfiguration<Transaction>`
- Adds unique constraint on **(BagId, OperationId, Currency, VendorId)**
  - Uses Fluent API: `.HasAlternateKey(t => new { t.BagId, t.Bags.Select(b => b.OperationId), t.Bags.Select(b => b.Currency), t.VendorId })`
  - **Note:** EF Core composite keys across owned collections require careful mapping; may use a computed unique index instead

**Repository:**
- Uses generic `IRepository<Transaction, Guid>` (inherited from base)
- No custom queries needed for initial scope

### API Layer

**VendorTransactionsController** (`Controllers/VendorTransactionsController.cs`):
- Route: `/api/vendor-transactions`
- Endpoint 1: `POST /T`
  - Extracts `X-Vendor-Id` header
  - Binds JSON body
  - Injects `VendorId` into `CreateTransactionTCommand`
  - Calls `CreateTransactionTCommandHandler.HandleAsync()`
  - Maps `Result` → HTTP response
- Endpoint 2: `POST /T+1`
  - Same flow as endpoint 1, but uses `CreateTransactionT1Command` and handler

**Response Mapping:**
- `Result.Success()` → `202 Accepted` with empty body
- `Result.Conflict()` → `409 Conflict` with standard problem details
- `ValidationException` (from FluentValidation) → `400 Bad Request` with validation errors (handled by middleware)

---

## Unique Constraint Implementation

The unique constraint applies to each **bag within a transaction**:
- **(BagId, OperationId, Currency, VendorId)** must be unique across all transactions in the database

**Implementation approach:**
- EF Core unique index on the Transaction/Bag relationship
- If multiple bags in a single request share the same (BagId, OperationId, Currency) → validation should catch this; alternatively, the index catches it at DB level
- On constraint violation → `DbUpdateException` is caught in the handler → `Result.Conflict()` returned

**Conflict handling:** Return `409 Conflict` without additional detail (per requirements).

---

## Data Flow

```
POST /api/vendor-transactions/T (or T+1)
  ↓
[Header Extraction: X-Vendor-Id]
  ↓
[Request Body Binding]
  ↓
[Create Command with VendorId]
  ↓
[Handler receives command]
  ↓
[FluentValidation runs (automatic)]
  ↓
Validation passes?
  ├─ NO → 400 Bad Request (error details)
  └─ YES → Continue
  ↓
[Create Transaction Entity, set Bags]
  ↓
[IUnitOfWork.SaveChangesAsync()]
  ↓
DbUpdateException (constraint violation)?
  ├─ YES → Result.Conflict() → 409 Conflict
  └─ NO → Result.Success() → 202 Accepted
```

---

## Error Handling

| Scenario | HTTP Status | Details |
|----------|------------|---------|
| Validation fails (DeclaredValue ≤ 0, etc.) | 400 | FluentValidation error details |
| Bag already exists (duplicate constraint) | 409 | No detail |
| Server error (unexpected exception) | 500 | Handled by ExceptionHandlingMiddleware |

---

## Testing Strategy

### Unit Tests
- **Validators:** Test each rule in isolation (T and T+1 scenarios)
- **Handlers:** Mock `IUnitOfWork`, test success and conflict paths
- **Entity:** Test Bag value object creation and constraints

### Integration Tests
- **Controller + Handler:** Test both endpoints with real DbContext
- **Constraint violation:** Insert a transaction, attempt duplicate bag, verify 409
- **Validation:** Send invalid payloads, verify 400 responses

### Manual Testing
- Use Postman/curl to POST to both endpoints
- Verify 202 on success, 409 on duplicate, 400 on validation failure
- Check OpenAPI schema includes both endpoints

---

## Future Extensibility

- **Additional transaction types:** Add `CreateTransactionT+2Command` following the same pattern (new command, new validator, new endpoint)
- **Custom queries:** If bag lookup by vendor/bagId is needed, extend the repository with a query handler
- **Audit trail:** Add created/updated timestamps to Transaction entity for compliance/debugging

---

## Implementation Order

1. Domain: `Transaction` entity and `Bag` value object
2. Application: Commands and validators
3. Infrastructure: Entity configuration with unique constraint; register handlers/validators in DI
4. Api: Controller with both endpoints
5. Tests: Unit + integration tests
6. Documentation: Update OpenAPI schema comments

---

## Open Questions / Assumptions

- **Header name:** Assuming `X-Vendor-Id`; can be changed if a different convention is preferred
- **Unique constraint implementation:** Using EF Core index; specific syntax depends on final entity design
- **Bag collection uniqueness within a request:** Assuming validation catches duplicates within the same request; DB constraint is final gate
- **Transaction status tracking:** Not included in initial scope; all transactions are recorded as-is

