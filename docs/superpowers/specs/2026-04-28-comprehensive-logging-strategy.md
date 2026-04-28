# Comprehensive Logging Strategy Specification

**Date:** 2026-04-28  
**Project:** Ingestion API  
**Scope:** Console and file logging for error diagnosis, observability, and transaction audit  
**Approach:** Middleware-centric with minimal handler instrumentation  

---

## Overview

This specification defines how logging will be implemented across the Ingestion API to provide:
- **Error Diagnosis:** Catch and debug issues in production
- **Observability:** Understand system flow, performance, and decisions
- **Audit/Compliance:** Track vendor transactions, validation results, and data changes

The system receives transactions from vendors and requires comprehensive audit trails. Logging is centralized in middleware to keep handler code clean while maintaining complete visibility.

---

## Architecture

### Layers

```
┌─────────────────────────────────────────────┐
│         HTTP Request                        │
├─────────────────────────────────────────────┤
│  1. Correlation ID Middleware               │
│     - Generate/retrieve correlation ID      │
│     - Add to LogContext                     │
├─────────────────────────────────────────────┤
│  2. Request/Response Logging Middleware     │
│     - Log request entry with method/path    │
│     - Capture transaction payload           │
│     - Log response with status/duration     │
├─────────────────────────────────────────────┤
│  3. Handler (with minimal logging)          │
│     - Log transaction-specific events only  │
│     - Validation results, completion status │
├─────────────────────────────────────────────┤
│  4. Exception Handling Middleware           │
│     - Catch unhandled exceptions            │
│     - Categorize by severity                │
│     - Log with full context                 │
├─────────────────────────────────────────────┤
│        HTTP Response                        │
└─────────────────────────────────────────────┘
```

### Middleware Components

#### 1. Correlation ID Middleware
**Purpose:** Ensure all logs related to a single request can be traced together.

**Behavior:**
- Check HTTP headers for existing correlation ID (X-Correlation-Id)
- If not present, generate a new GUID
- Store in `HttpContext.Items["CorrelationId"]`
- Add to Serilog `LogContext.PushProperty("CorrelationId", correlationId)`
- Add to HTTP response header `X-Correlation-Id` for vendor visibility

**Result:** All logs from this request automatically include the correlation ID without handler code.

#### 2. Request/Response Logging Middleware
**Purpose:** Log HTTP traffic, transaction payloads, and performance.

**On Request Entry:**
```
Log: "{Method} {Path} started"
Properties: CorrelationId, Method, Path, VendorId (if available)
Level: Information
```

For transaction endpoints (e.g., POST /api/transactions), capture request body to log transaction payload:
```
Log: "Transaction payload received: {@Payload}"
Properties: CorrelationId, TransactionId, VendorId
Level: Information
```

**On Request Exit:**
```
Log: "{Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}"
Properties: CorrelationId, Method, Path, StatusCode, ElapsedMs
Level: Information (200-399), Warning (400-499), Error (500+)
```

**Audit Trail:** Request and response logs together form a complete transaction audit record.

#### 3. Handler-Level Logging (Minimal)
**Purpose:** Log transaction-specific business events that middleware cannot capture.

**When to log in handlers:**
- Transaction received and parsing started
- Validation passed/failed with specific error details
- Processing status changes
- Successful completion

**Example in Command Handler:**
```csharp
public async Task<Result> HandleAsync(CreateTransactionCommand cmd, CancellationToken ct)
{
    _logger.LogInformation("Transaction received from vendor {VendorId} with ID {TransactionId}", 
        cmd.VendorId, cmd.TransactionId);
    
    var validator = new CreateTransactionValidator();
    var validation = validator.Validate(cmd);
    
    if (!validation.IsValid)
    {
        _logger.LogWarning("Transaction {TransactionId} validation failed: {@ValidationErrors}", 
            cmd.TransactionId, validation.Errors);
        return Result.Failure("Validation failed");
    }
    
    // Process transaction
    
    _logger.LogInformation("Transaction {TransactionId} processed successfully", cmd.TransactionId);
    return Result.Success();
}
```

**Keep it minimal:** Log only at critical points (received, validated, completed). Don't log inside loops or repetitive operations.

#### 4. Exception Handling Middleware
**Purpose:** Catch unhandled exceptions, categorize by severity, and log with full context.

**Exception Categorization by Severity:**

| Category | Examples | Log Level | HTTP Status |
|----------|----------|-----------|-------------|
| **Critical** | Database connection failure, system crash | Fatal | 500 |
| **High** | Validation failure, business rule violation, transaction rejected | Error | 400/422 |
| **Medium** | Timeout, retry exhausted, dependency unavailable | Warning | 503 |
| **Low** | Info-level diagnostics, expected operational issues | Information | 200 |

**Logging on Exception:**
```
Log: "Unhandled exception: {ExceptionType}: {ExceptionMessage}"
Properties: CorrelationId, ExceptionType, StackTrace, Severity, RequestPath
Level: Based on categorization (Fatal/Error/Warning/Information)
```

---

## Serilog Configuration

### Current Configuration (appsettings.json)

Already correct, no changes needed:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "WriteTo": [
    { "Name": "Console" },
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
```

### Structured Logging Properties

All logs automatically include via LogContext enrichment:

| Property | Source | Purpose |
|----------|--------|---------|
| CorrelationId | Correlation ID Middleware | Trace related logs |
| RequestPath | Request/Response Middleware | Identify endpoint |
| ElapsedMs | Request/Response Middleware | Track performance |
| VendorId | Handler or request metadata | Filter by vendor |
| TransactionId | Handler | Filter by transaction |
| Severity | Exception Middleware | Categorize exceptions |
| Timestamp | Serilog (automatic) | Event timeline |
| Level | Serilog (automatic) | Severity level |

---

## Implementation Details

### File Structure
```
src/IngestionApi.Api/
├── Middleware/
│   ├── CorrelationIdMiddleware.cs
│   ├── RequestResponseLoggingMiddleware.cs
│   └── ExceptionHandlingMiddleware.cs
└── Program.cs (register middleware in order)
```

### Middleware Registration Order (in Program.cs)
```csharp
// After building the app, before UseRouting:
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
```

**Order matters:** Exception handling outermost to catch all exceptions, then correlation ID, then request logging.

### Handler Injection Pattern

Inject `ILogger<T>` in command/query handlers:
```csharp
public class CreateTransactionCommandHandler : ICommandHandler<CreateTransactionCommand, Result>
{
    private readonly ILogger<CreateTransactionCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;
    
    public CreateTransactionCommandHandler(
        ILogger<CreateTransactionCommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }
    
    // ... rest of handler
}
```

DI container automatically resolves `ILogger<T>` — no registration needed.

---

## Logging Coverage

### By Component

| Component | What to Log | When | Log Level |
|-----------|------------|------|-----------|
| Correlation Middleware | Correlation ID created/retrieved | Every request | Information |
| Request/Response Middleware | Request/response details, duration | Every request/response | Information/Warning/Error |
| Handlers | Transaction events, validation results | At critical points | Information/Warning |
| Exception Middleware | Unhandled exceptions with context | When exception occurs | Fatal/Error/Warning |

### Audit Trail for Vendor Transactions

A single transaction request generates this log sequence:

1. **Request entry:** `POST /api/transactions started` (middleware)
2. **Payload received:** `Transaction payload received from vendor X` (middleware)
3. **Handler start:** `Transaction received from vendor X with ID Y` (handler)
4. **Validation:** `Transaction Y validation passed` or `validation failed: [errors]` (handler)
5. **Processing:** `Transaction Y processed successfully` (handler)
6. **Response:** `POST /api/transactions completed in 145ms with status 200` (middleware)

All logs tied together by CorrelationId. If validation fails, error logs appear in step 4. If unhandled exception, Exception Middleware catches and logs before response.

---

## Performance Considerations

- **Minimal overhead:** Middleware is thin; structured logging properties are fast
- **No synchronous I/O:** All logging is async by default (Serilog)
- **File rolling:** Daily rolling prevents log files from growing unbounded
- **LogContext enrichment:** No performance penalty; Serilog handles efficiently
- **Request body capture:** Only for transaction endpoints, not all requests

---

## Exception Categorization Reference

### Critical (LogLevel.Fatal) → HTTP 500
- `InvalidOperationException` (system in invalid state)
- `NullReferenceException` (null reference bug)
- Database connection failures
- Configuration errors

### High (LogLevel.Error) → HTTP 400/422
- `ValidationException` (invalid input)
- `ArgumentException` (bad arguments)
- Business rule violations
- Data integrity violations

### Medium (LogLevel.Warning) → HTTP 503
- `TimeoutException`
- `IOException` (temporary I/O issue)
- Retry exhausted
- Resource unavailable

### Low (LogLevel.Information)
- Normal flow diagnostics
- Expected operational events
- Performance milestones

---

## Success Criteria

✅ All HTTP requests logged with method, path, status, duration  
✅ All vendor transactions logged with payload and validation results  
✅ All unhandled exceptions logged with categorization by severity  
✅ Correlation ID automatically propagates to all related logs  
✅ Logs rolled daily with timestamp in filename  
✅ Console output for Development, file output for all environments  
✅ Handler code remains clean (minimal logging)  
✅ Complete audit trail for compliance (vendor ID, transaction ID, status, errors)  

---

## Future Enhancements (Out of Scope)

- Database query logging via EF Core interceptors
- Request/response body archival (separate blob storage)
- Log aggregation to centralized system (e.g., ELK, DataDog)
- Custom metrics/counters (request rates, error rates)
- Distributed tracing across multiple services (parent/child span IDs)

These can be added later without changing the current middleware structure.
