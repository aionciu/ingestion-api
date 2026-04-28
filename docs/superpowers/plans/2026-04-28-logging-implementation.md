# Comprehensive Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement middleware-centric logging for error diagnosis, observability, and transaction audit with minimal handler instrumentation.

**Architecture:** Three middleware components (Correlation ID, Request/Response, Exception Handling) handle centralized logging. Serilog already configured. Handlers log only business events with minimal code.

**Tech Stack:** Serilog, .NET Core logging abstractions (ILogger), middleware pattern, structured logging.

---

## File Structure

**New files to create:**
```
src/IngestionApi.Api/Middleware/
├── CorrelationIdMiddleware.cs
├── RequestResponseLoggingMiddleware.cs
└── ExceptionHandlingMiddleware.cs
```

**Files to modify:**
```
src/IngestionApi.Api/Program.cs (register middleware)
src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionCommandHandler.cs (add logging)
```

**Existing (no changes needed):**
```
src/IngestionApi.Api/appsettings.json (Serilog already configured correctly)
```

---

## Task 1: Create Correlation ID Middleware

**Files:**
- Create: `src/IngestionApi.Api/Middleware/CorrelationIdMiddleware.cs`

**Steps:**

- [ ] **Step 1: Create middleware class with interface implementation**

Create the file `src/IngestionApi.Api/Middleware/CorrelationIdMiddleware.cs`:

```csharp
using Serilog.Context;

namespace IngestionApi.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        LogContext.PushProperty("CorrelationId", correlationId);

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        await _next(context);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/IngestionApi.Api/Middleware/CorrelationIdMiddleware.cs
git commit -m "feat: add correlation ID middleware

- Generate/retrieve correlation ID from request headers
- Store in HttpContext and LogContext for automatic enrichment
- Return correlation ID in response headers for vendor visibility"
```

---

## Task 2: Register Correlation ID Middleware

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs`

**Steps:**

- [ ] **Step 1: Add middleware registration after app.Build()**

In `src/IngestionApi.Api/Program.cs`, find the line `var app = builder.Build();` and add the middleware registration after it:

```csharp
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Program.cs
git commit -m "feat: register correlation ID middleware in pipeline"
```

---

## Task 3: Create Request/Response Logging Middleware

**Files:**
- Create: `src/IngestionApi.Api/Middleware/RequestResponseLoggingMiddleware.cs`

**Steps:**

- [ ] **Step 1: Create middleware class**

Create the file `src/IngestionApi.Api/Middleware/RequestResponseLoggingMiddleware.cs`:

```csharp
using System.Diagnostics;

namespace IngestionApi.Api.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;

        _logger.LogInformation("{Method} {Path} started", method, path);

        // For transaction endpoints, capture request body for audit
        if (IsTransactionEndpoint(path))
        {
            await CaptureRequestBody(context);
        }

        var stopwatch = Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        var statusCode = context.Response.StatusCode;
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        var logLevel = statusCode >= 500 ? LogLevel.Error
                     : statusCode >= 400 ? LogLevel.Warning
                     : LogLevel.Information;

        _logger.Log(logLevel, "{Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
            method, path, elapsedMs, statusCode);
    }

    private static bool IsTransactionEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/transactions", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CaptureRequestBody(HttpContext context)
    {
        context.Request.EnableBuffering();
        var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var logger = context.RequestServices.GetRequiredService<ILogger<RequestResponseLoggingMiddleware>>();
        logger.LogInformation("Transaction payload received: {Payload}", body);
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Middleware/RequestResponseLoggingMiddleware.cs
git commit -m "feat: add request/response logging middleware

- Log HTTP method, path on request entry
- Capture transaction payload for audit endpoints
- Log response status and elapsed time on exit
- Log level based on status code (Error 5xx, Warning 4xx, Info 2xx)"
```

---

## Task 4: Register Request/Response Logging Middleware

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs`

**Steps:**

- [ ] **Step 1: Add middleware registration after CorrelationId**

In `src/IngestionApi.Api/Program.cs`, update the middleware registrations:

```csharp
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Program.cs
git commit -m "feat: register request/response logging middleware"
```

---

## Task 5: Create Exception Handling Middleware

**Files:**
- Create: `src/IngestionApi.Api/Middleware/ExceptionHandlingMiddleware.cs`

**Steps:**

- [ ] **Step 1: Create middleware class with exception categorization**

Create the file `src/IngestionApi.Api/Middleware/ExceptionHandlingMiddleware.cs`:

```csharp
using System.Text.Json;
using FluentValidation;

namespace IngestionApi.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (severity, statusCode) = CategorizeException(exception);

        _logger.Log(severity, exception, "Unhandled exception: {ExceptionType}", exception.GetType().Name);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new { error = exception.Message };
        await context.Response.WriteAsJsonAsync(response);
    }

    private static (LogLevel, int) CategorizeException(Exception exception) => exception switch
    {
        // Critical (Fatal) - system in invalid state
        InvalidOperationException => (LogLevel.Critical, StatusCodes.Status500InternalServerError),
        NullReferenceException => (LogLevel.Critical, StatusCodes.Status500InternalServerError),

        // High (Error) - validation/business rule failures
        ValidationException => (LogLevel.Error, StatusCodes.Status422UnprocessableEntity),
        ArgumentException => (LogLevel.Error, StatusCodes.Status400BadRequest),

        // Medium (Warning) - temporary operational issues
        TimeoutException => (LogLevel.Warning, StatusCodes.Status503ServiceUnavailable),
        IOException => (LogLevel.Warning, StatusCodes.Status503ServiceUnavailable),

        // Default - treat as error
        _ => (LogLevel.Error, StatusCodes.Status500InternalServerError)
    };
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Middleware/ExceptionHandlingMiddleware.cs
git commit -m "feat: add exception handling middleware with severity categorization

- Categorize exceptions: Critical (Fatal), High (Error), Medium (Warning)
- Log full exception with context
- Return appropriate HTTP status code
- Categorization: validation errors 422, timeouts 503, others 500"
```

---

## Task 6: Register Exception Handling Middleware

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs`

**Steps:**

- [ ] **Step 1: Add exception middleware as outermost middleware**

In `src/IngestionApi.Api/Program.cs`, update to register exception handling first (outermost):

```csharp
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Program.cs
git commit -m "feat: register exception handling middleware as outermost layer"
```

---

## Task 7: Add ILogger to a Sample Handler

**Files:**
- Modify: `src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionCommandHandler.cs` (if exists, or create if needed)

**Steps:**

- [ ] **Step 1: Check if handler exists**

Run: `find src/IngestionApi.Application -name "*CommandHandler.cs" -o -name "*QueryHandler.cs" | head -5`

This will show existing handlers. If `CreateTransactionCommandHandler.cs` doesn't exist, we'll create a sample.

- [ ] **Step 2: If handler exists, add ILogger injection**

Open the existing handler and update the constructor. For example, if you have `CreateTransactionCommandHandler`:

```csharp
using IngestionApi.Application.Common;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

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

    public async Task<Result> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Transaction received from vendor {VendorId} with ID {TransactionId}",
            command.VendorId, command.TransactionId);

        // TODO: Add validation logic
        
        // TODO: Process transaction

        _logger.LogInformation("Transaction {TransactionId} processed successfully", command.TransactionId);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

**If handler doesn't exist**, create a sample:

```csharp
using IngestionApi.Application.Common;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

public record CreateTransactionCommand(string VendorId, string TransactionId, string Payload);

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

    public async Task<Result> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Transaction received from vendor {VendorId} with ID {TransactionId}",
            command.VendorId, command.TransactionId);

        // Placeholder: validation and processing would go here

        _logger.LogInformation("Transaction {TransactionId} processed successfully", command.TransactionId);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build`
Expected: Build succeeds with no errors

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Application/Features/Transactions/Commands/CreateTransactionCommandHandler.cs
git commit -m "feat: add ILogger to CreateTransactionCommandHandler

- Inject ILogger<T> in constructor
- Log transaction received with VendorId and TransactionId
- Log successful processing completion
- Minimal instrumentation at critical business points"
```

---

## Task 8: Add ILogger to Other Handlers

**Files:**
- Modify: `src/IngestionApi.Application/Features/**/*CommandHandler.cs`
- Modify: `src/IngestionApi.Application/Features/**/*QueryHandler.cs`

**Steps:**

- [ ] **Step 1: Find all handlers in the application**

Run: `find src/IngestionApi.Application -name "*CommandHandler.cs" -o -name "*QueryHandler.cs"`

Expected: List of all handler files

- [ ] **Step 2: For each handler (except CreateTransactionCommandHandler which was done in Task 7), add ILogger injection and minimal logging**

For each handler, follow this pattern:

```csharp
using IngestionApi.Application.Common;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.SomeFeature.Commands;

public class SomeCommandHandler : ICommandHandler<SomeCommand, Result>
{
    private readonly ILogger<SomeCommandHandler> _logger;
    // ... other dependencies ...

    public SomeCommandHandler(
        ILogger<SomeCommandHandler> logger,
        // ... other dependencies ...)
    {
        _logger = logger;
        // ... assign other dependencies ...
    }

    public async Task<Result> HandleAsync(SomeCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing {CommandType}", typeof(SomeCommand).Name);

        // ... handler logic ...

        _logger.LogInformation("{CommandType} processed successfully", typeof(SomeCommand).Name);
        return Result.Success();
    }
}
```

**Logging pattern:** Keep it minimal:
- One log at entry with what's being processed
- One log at successful exit
- Validation failures logged separately (in next task if applicable)

- [ ] **Step 3: Run tests to ensure nothing broke**

Run: `dotnet test`
Expected: All tests pass (or same status as before if tests were failing)

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Application/Features/
git commit -m "feat: add ILogger to all command and query handlers

- Inject ILogger<T> in constructor (auto-resolved by DI)
- Log handler entry with command/query name
- Log successful completion
- Keep logging minimal at critical points"
```

---

## Task 9: Integration Test for Logging Flow

**Files:**
- Create: `tests/IngestionApi.Tests.Integration/Logging/LoggingIntegrationTests.cs`

**Steps:**

- [ ] **Step 1: Create integration test class**

Create the file `tests/IngestionApi.Tests.Integration/Logging/LoggingIntegrationTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IngestionApi.Tests.Integration.Logging;

public class LoggingIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task HealthCheck_ReturnsOkAndLogsRequest()
    {
        // Arrange
        var endpoint = "/health"; // Adjust based on actual endpoint

        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task CorrelationIdPropagates_InRequestAndResponse()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health"); // Adjust endpoint
        request.Headers.Add("X-Correlation-Id", expectedCorrelationId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var returnedCorrelationId = values.First();
        Assert.Equal(expectedCorrelationId, returnedCorrelationId);
    }

    [Fact]
    public async Task NewCorrelationIdGenerated_IfNotProvided()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health"); // Adjust endpoint

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var correlationId = values.First();
        Assert.NotEmpty(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }
}
```

- [ ] **Step 2: Create test project structure if needed**

Run: `mkdir -p tests/IngestionApi.Tests.Integration/Logging`

Expected: Directory created (or already exists)

- [ ] **Step 3: Build and run the test**

Run: `dotnet test tests/IngestionApi.Tests.Integration/Logging/LoggingIntegrationTests.cs -v`

Expected: Tests pass (or fail if health endpoint doesn't exist—adjust test to match actual endpoint)

- [ ] **Step 4: Commit**

```bash
git add tests/IngestionApi.Tests.Integration/Logging/LoggingIntegrationTests.cs
git commit -m "test: add integration tests for logging flow

- Test correlation ID generation and propagation
- Test correlation ID in response headers
- Test custom correlation ID is preserved"
```

---

## Task 10: Manual Testing & Verification

**Files:**
- No files created; manual verification only

**Steps:**

- [ ] **Step 1: Build and run the API**

Run: `dotnet build && dotnet run --project src/IngestionApi.Api`

Expected: API starts with no errors, logs appear in console showing startup

- [ ] **Step 2: Make a test request to see logging in action**

In another terminal, run:
```bash
curl -X GET http://localhost:5000/health \
  -H "X-Correlation-Id: test-correlation-123" \
  -v
```

(Adjust port and endpoint as needed)

Expected output in API console:
```
2026-04-28 10:30:45 [INF] GET /health started {CorrelationId=test-correlation-123}
2026-04-28 10:30:45 [INF] GET /health completed in 5ms with status 200 {CorrelationId=test-correlation-123}
```

- [ ] **Step 3: Test correlation ID generation**

Run:
```bash
curl -X GET http://localhost:5000/health -v
```

Expected output includes:
- Response header: `X-Correlation-Id: <generated-guid>`
- Logs show the same correlation ID

- [ ] **Step 4: Test exception handling (if error endpoint exists)**

Run a request that would cause an error (e.g., POST with invalid data):
```bash
curl -X POST http://localhost:5000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{"invalid": "data"}' \
  -v
```

Expected:
- Error logged with categorization (Error level for validation)
- Appropriate HTTP status (422 for validation)
- Correlation ID in logs and response headers

- [ ] **Step 5: Verify log files are being created**

Run:
```bash
ls -la logs/
cat logs/ingestion-*.log | tail -20
```

Expected: Log file exists with timestamp, contains logged entries with CorrelationId

- [ ] **Step 6: No commit needed for manual testing**

Manual testing verification is complete. All logging should be working end-to-end.

---

## Verification Checklist

After implementation, verify:

- [ ] All three middleware classes created and registered in correct order
- [ ] CorrelationId middleware generates new ID or uses provided header
- [ ] CorrelationId included in response headers
- [ ] Request/response logging includes method, path, status, duration
- [ ] Exception handling middleware catches exceptions and categorizes by severity
- [ ] All handlers have ILogger injected
- [ ] Handler logging includes business events (received, processing status)
- [ ] Serilog logs to both console and file with daily rolling
- [ ] Integration tests verify correlation ID propagation
- [ ] Manual request produces log entries with correlation ID
- [ ] Log files created in `logs/` directory with timestamp

---

## Notes

- **Serilog configuration:** No changes needed; already correct in appsettings.json
- **DI resolution:** ILogger<T> is automatically resolved by the framework; no manual registration needed
- **LogContext:** Serilog automatically includes LogContext properties in structured logs
- **Performance:** Middleware is lightweight; exception middleware only active on errors
- **Future enhancements:** Database query logging, log aggregation, distributed tracing can be added without changing middleware

