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
  "operationId": "string",
  "bagId": "string",
  "bags": [
    {
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

## Data Model Transformation

**Key behavior:** Each bag in the request is expanded into **3 separate transaction rows** in the database:
- One row for DeclaredValue (TransactionType = 1)
- One row for ProcessedValue (TransactionType = 2)
- One row for DifferenceValue (TransactionType = 3)

**Example:**
```
Request: { operationId: "OP002", bagId: "BAG002", bags: [{ declaredValue: 1000, processedValue: 500, differenceValue: 100, currency: "EUR" }] }

Database creates 3 rows:
Row 1: { operationId: "OP002", bagId: "BAG002", vendorId: "V123", currency: "EUR", value: 1000, transactionType: 1, ... }
Row 2: { operationId: "OP002", bagId: "BAG002", vendorId: "V123", currency: "EUR", value: 500, transactionType: 2, ... }
Row 3: { operationId: "OP002", bagId: "BAG002", vendorId: "V123", currency: "EUR", value: 100, transactionType: 3, ... }
```

---

## Architecture & Design

### Domain Layer

**Transaction Entity** (`Domain/Entities/Transaction.cs`):
- Primary key: `Id` (Guid)
- `OperationId` (string) — from request root
- `VendorId` (string) — from header, stored in DB
- `BagId` (string) — root-level bag identifier (money identification)
- `Currency` (string) — from bag
- `Value` (decimal) — the monetary value (DeclaredValue, ProcessedValue, or DifferenceValue)
- `TransactionType` (int) — 1=DeclaredValue, 2=ProcessedValue, 3=DifferenceValue
- `CreatedAt` (DateTime) — set on insert
- `ModifiedAt` (DateTime) — set on insert, updated on any modification

**Bag Value Object** (`Domain/ValueObjects/Bag.cs`):
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
3. **Expand each bag into 3 separate Transaction entities:**
   - Create Transaction with Value=DeclaredValue, TransactionType=1
   - Create Transaction with Value=ProcessedValue, TransactionType=2
   - Create Transaction with Value=DifferenceValue, TransactionType=3
4. Persist all transactions via `IUnitOfWork.SaveChangesAsync()`
5. Catch `DbUpdateException` (from duplicate constraint violation) → return `Result.Conflict()`
6. Return `Result.Success()` on success

### Infrastructure Layer

**Entity Configuration** (`Persistence/Configurations/TransactionConfiguration.cs`):
- Configures `Transaction` via `IEntityTypeConfiguration<Transaction>`
- Maps properties: Id (key), OperationId, VendorId, BagId, Currency, Value, TransactionType, CreatedAt, ModifiedAt
- Adds unique constraint on **(BagId, OperationId, Currency, VendorId, TransactionType)**
  - Since each bag expands into 3 rows (one per TransactionType), this ensures no duplicate (bag, operation, currency, vendor, type) combinations
  - Implemented via EF Core unique index on these five columns

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
- OperationId is shared across all bags in a request (root level)
- Each bag specifies its own Currency

**Implementation approach:**
- EF Core unique index on the Bag's (BagId, OperationId, Currency, VendorId) composite
- If multiple bags in a single request have the same Currency → validation may catch duplicates; DB constraint is the final gate
- On constraint violation → `DbUpdateException` is caught in the handler → `Result.Conflict()` returned

**Conflict handling:** Return `409 Conflict` without additional detail (per requirements).

---

## Data Flow

```
POST /api/vendor-transactions/T (or T+1)
  ↓
[Header Extraction: X-Vendor-Id]
  ↓
[Request Body Binding: bags array]
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
[For each bag, create 3 Transaction entities]
  - Transaction 1: Value=DeclaredValue, TransactionType=1
  - Transaction 2: Value=ProcessedValue, TransactionType=2
  - Transaction 3: Value=DifferenceValue, TransactionType=3
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
- **Unique constraint implementation:** Using EF Core index on (BagId, OperationId, Currency, VendorId)
- **Bag collection uniqueness within a request:** Multiple bags with the same Currency in a single request will fail the DB constraint
- **Transaction status tracking:** Not included in initial scope; all transactions are recorded as-is
- **Timestamp generation:** CreatedAt and ModifiedAt are set on entity creation and tracked by EF Core

