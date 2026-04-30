# 401 Forensic Audit Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dedicated middleware that, on every HTTP 401 response, emits a structured Serilog warning containing the full request envelope (method, path, query, source IP, redacted headers, JSON body capped at 8 KB).

**Architecture:** New `AuthFailureAuditMiddleware` in the Api layer, registered in `Program.cs` between `RequestResponseLoggingMiddleware` and `UseAuthorization()`. Existing `VendorSignatureFilter` and `RequestResponseLoggingMiddleware` are not modified. Body buffering is enabled before `_next`; on 401 the request stream is rewound and read up to a fixed cap. Sensitive headers (`Authorization`, `Cookie`, `Set-Cookie`, `Proxy-Authorization`) are masked; `X-Vendor-Signature` is truncated to a prefix + length.

**Tech Stack:** ASP.NET Core 10 middleware, Serilog (already configured), xUnit (existing tests).

**Spec:** `docs/superpowers/specs/2026-04-29-auth-failure-audit-logging-design.md`

---

## File Structure

- Create: `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs` — the middleware (single class, with private static helpers `RedactHeaders` and `ReadBodyAsync` and constants `MaxBodyBytes`, `SensitiveHeaders`, `SignatureHeader`).
- Modify: `src/IngestionApi.Api/Program.cs` — register the new middleware after `RequestResponseLoggingMiddleware`.
- Create: `tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs` — unit tests using `DefaultHttpContext` plus a small in-memory `ILogger<T>` capture (xUnit + a test logger; pattern follows existing `RequestResponseLoggingMiddlewareTests`).

---

## Task 1: Middleware skeleton — no-op on non-401

**Files:**
- Create: `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs`
- Test: `tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs`

- [ ] **Step 1: Create the test file with a failing "no log on 200" test**

```csharp
using IngestionApi.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Tests.Unit.Middleware;

public class AuthFailureAuditMiddlewareTests
{
    private sealed class CapturingLogger : ILogger<AuthFailureAuditMiddleware>
    {
        public List<(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var kvps = state as IReadOnlyList<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            Entries.Add((logLevel, formatter(state, exception), kvps));
        }
    }

    private static async Task<(HttpContext Ctx, CapturingLogger Log)> Run(
        int status,
        Action<HttpContext>? configureRequest = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/x";
        configureRequest?.Invoke(ctx);

        var logger = new CapturingLogger();
        var mw = new AuthFailureAuditMiddleware(c =>
        {
            c.Response.StatusCode = status;
            return Task.CompletedTask;
        }, logger);
        await mw.InvokeAsync(ctx);
        return (ctx, logger);
    }

    [Fact]
    public async Task NonUnauthorized_EmitsNoAuditRecord()
    {
        var (_, log) = await Run(200);
        Assert.Empty(log.Entries);
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compile error)**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: FAIL — `AuthFailureAuditMiddleware` does not exist.

- [ ] **Step 3: Create the middleware skeleton**

Create `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs`:

```csharp
namespace IngestionApi.Api.Middleware;

public class AuthFailureAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthFailureAuditMiddleware> _logger;

    public AuthFailureAuditMiddleware(RequestDelegate next, ILogger<AuthFailureAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
        {
            return;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs
git commit -m "feat(api): scaffold AuthFailureAuditMiddleware (no-op on non-401)"
```

---

## Task 2: Emit basic audit record on 401 (envelope fields, no headers/body yet)

**Files:**
- Modify: `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs`
- Test: `tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs`

- [ ] **Step 1: Add a failing test for the 401 envelope log**

Append to `AuthFailureAuditMiddlewareTests`:

```csharp
[Fact]
public async Task Unauthorized_EmitsWarningWithEnvelopeFields()
{
    var (_, log) = await Run(401, ctx =>
    {
        ctx.Request.QueryString = new QueryString("?a=1");
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
    });

    var entry = Assert.Single(log.Entries);
    Assert.Equal(LogLevel.Warning, entry.Level);

    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    Assert.Equal("POST", dict["Method"]);
    Assert.Equal("/api/x", dict["Path"]);
    Assert.Equal("?a=1", dict["QueryString"]);
    Assert.Equal("10.0.0.5", dict["RemoteIp"]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~Unauthorized_EmitsWarningWithEnvelopeFields"`
Expected: FAIL — no log entries are recorded.

- [ ] **Step 3: Implement the envelope log**

Replace the body of `InvokeAsync` in `AuthFailureAuditMiddleware.cs`:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    context.Request.EnableBuffering();
    await _next(context);

    if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
    {
        return;
    }

    var request = context.Request;
    var method = request.Method;
    var path = request.Path.Value ?? string.Empty;
    var queryString = request.QueryString.Value ?? string.Empty;
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    _logger.LogWarning(
        "401 audit: {Method} {Path}{QueryString} from {RemoteIp}",
        method, path, queryString, remoteIp);
}
```

- [ ] **Step 4: Run all middleware tests**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs
git commit -m "feat(api): emit envelope audit log on 401"
```

---

## Task 3: Header collection with redaction

**Files:**
- Modify: `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs`
- Test: `tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs`

- [ ] **Step 1: Add failing tests for header redaction**

Append:

```csharp
[Fact]
public async Task Unauthorized_RedactsSensitiveHeaders()
{
    var (_, log) = await Run(401, ctx =>
    {
        ctx.Request.Headers["Authorization"] = "Bearer secret";
        ctx.Request.Headers["Cookie"] = "session=abc";
        ctx.Request.Headers["X-Vendor-Id"] = "vendor-001";
    });

    var entry = Assert.Single(log.Entries);
    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    var headers = Assert.IsAssignableFrom<IDictionary<string, string>>(dict["Headers"]);

    Assert.Equal("***", headers["Authorization"]);
    Assert.Equal("***", headers["Cookie"]);
    Assert.Equal("vendor-001", headers["X-Vendor-Id"]);
}

[Fact]
public async Task Unauthorized_TruncatesVendorSignature()
{
    var sig = new string('A', 200);
    var (_, log) = await Run(401, ctx =>
    {
        ctx.Request.Headers["X-Vendor-Signature"] = sig;
    });

    var entry = Assert.Single(log.Entries);
    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    var headers = (IDictionary<string, string>)dict["Headers"]!;

    Assert.Equal("AAAAAAAA…(len=200)", headers["X-Vendor-Signature"]);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: FAIL — `Headers` not in state.

- [ ] **Step 3: Implement header redaction**

Update `AuthFailureAuditMiddleware.cs` so the full file reads:

```csharp
namespace IngestionApi.Api.Middleware;

public class AuthFailureAuditMiddleware
{
    private const string SignatureHeader = "X-Vendor-Signature";
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuthFailureAuditMiddleware> _logger;

    public AuthFailureAuditMiddleware(RequestDelegate next, ILogger<AuthFailureAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
        {
            return;
        }

        var request = context.Request;
        var method = request.Method;
        var path = request.Path.Value ?? string.Empty;
        var queryString = request.QueryString.Value ?? string.Empty;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var headers = RedactHeaders(request.Headers);

        _logger.LogWarning(
            "401 audit: {Method} {Path}{QueryString} from {RemoteIp} headers={@Headers}",
            method, path, queryString, remoteIp, headers);
    }

    private static IDictionary<string, string> RedactHeaders(IHeaderDictionary source)
    {
        var result = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in source)
        {
            if (SensitiveHeaders.Contains(name))
            {
                result[name] = "***";
                continue;
            }

            var raw = string.Join(", ", values.ToArray());

            if (string.Equals(name, SignatureHeader, StringComparison.OrdinalIgnoreCase) && raw.Length > 8)
            {
                result[name] = $"{raw[..8]}…(len={raw.Length})";
                continue;
            }

            result[name] = raw;
        }
        return result;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs
git commit -m "feat(api): redact sensitive headers in 401 audit log"
```

---

## Task 4: JSON body capture with 8 KB cap and content-type guard

**Files:**
- Modify: `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs`
- Test: `tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs`

- [ ] **Step 1: Add failing tests covering body cases**

Append:

```csharp
private static void SetJsonBody(HttpContext ctx, string body)
{
    var bytes = System.Text.Encoding.UTF8.GetBytes(body);
    ctx.Request.ContentType = "application/json";
    ctx.Request.ContentLength = bytes.Length;
    ctx.Request.Body = new MemoryStream(bytes);
}

[Fact]
public async Task Unauthorized_LogsJsonBodyVerbatim_WhenSmall()
{
    const string body = "{\"id\":1}";
    var (_, log) = await Run(401, ctx => SetJsonBody(ctx, body));

    var entry = Assert.Single(log.Entries);
    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    Assert.Equal(body, dict["Body"]);
    Assert.Equal(false, dict["BodyTruncated"]);
    Assert.Equal((long)body.Length, dict["BodyTotalBytes"]);
}

[Fact]
public async Task Unauthorized_TruncatesBody_WhenLargerThan8Kb()
{
    var body = new string('x', 10000);
    var (_, log) = await Run(401, ctx => SetJsonBody(ctx, body));

    var entry = Assert.Single(log.Entries);
    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    var logged = Assert.IsType<string>(dict["Body"]);
    Assert.Equal(8192, System.Text.Encoding.UTF8.GetByteCount(logged));
    Assert.Equal(true, dict["BodyTruncated"]);
    Assert.Equal((long)10000, dict["BodyTotalBytes"]);
}

[Fact]
public async Task Unauthorized_OmitsNonJsonBody()
{
    var (_, log) = await Run(401, ctx =>
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02 };
        ctx.Request.ContentType = "application/octet-stream";
        ctx.Request.ContentLength = bytes.Length;
        ctx.Request.Body = new MemoryStream(bytes);
    });

    var entry = Assert.Single(log.Entries);
    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    Assert.Equal("[non-json content omitted]", dict["Body"]);
    Assert.Equal(false, dict["BodyTruncated"]);
    Assert.Equal((long)3, dict["BodyTotalBytes"]);
}

[Fact]
public async Task Unauthorized_HandlesEmptyBody()
{
    var (_, log) = await Run(401);

    var entry = Assert.Single(log.Entries);
    var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    Assert.Equal(string.Empty, dict["Body"]);
    Assert.Equal(false, dict["BodyTruncated"]);
    Assert.Equal((long)0, dict["BodyTotalBytes"]);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: FAIL — `Body`, `BodyTruncated`, `BodyTotalBytes` not in state.

- [ ] **Step 3: Implement body capture**

Replace `AuthFailureAuditMiddleware.cs` with the final form:

```csharp
namespace IngestionApi.Api.Middleware;

public class AuthFailureAuditMiddleware
{
    private const int MaxBodyBytes = 8192;
    private const string SignatureHeader = "X-Vendor-Signature";
    private const string JsonContentTypePrefix = "application/json";
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuthFailureAuditMiddleware> _logger;

    public AuthFailureAuditMiddleware(RequestDelegate next, ILogger<AuthFailureAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
        {
            return;
        }

        var request = context.Request;
        var method = request.Method;
        var path = request.Path.Value ?? string.Empty;
        var queryString = request.QueryString.Value ?? string.Empty;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var headers = RedactHeaders(request.Headers);
        var (body, truncated, totalBytes) = await ReadBodyAsync(request);

        _logger.LogWarning(
            "401 audit: {Method} {Path}{QueryString} from {RemoteIp} headers={@Headers} body={Body} truncated={BodyTruncated} totalBytes={BodyTotalBytes}",
            method, path, queryString, remoteIp, headers, body, truncated, totalBytes);
    }

    private static IDictionary<string, string> RedactHeaders(IHeaderDictionary source)
    {
        var result = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in source)
        {
            if (SensitiveHeaders.Contains(name))
            {
                result[name] = "***";
                continue;
            }

            var raw = string.Join(", ", values.ToArray());

            if (string.Equals(name, SignatureHeader, StringComparison.OrdinalIgnoreCase) && raw.Length > 8)
            {
                result[name] = $"{raw[..8]}…(len={raw.Length})";
                continue;
            }

            result[name] = raw;
        }
        return result;
    }

    private static async Task<(string Body, bool Truncated, long TotalBytes)> ReadBodyAsync(HttpRequest request)
    {
        var contentLength = request.ContentLength ?? 0;
        if (contentLength == 0)
        {
            return (string.Empty, false, 0);
        }

        var contentType = request.ContentType ?? string.Empty;
        if (!contentType.StartsWith(JsonContentTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ("[non-json content omitted]", false, contentLength);
        }

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        var buffer = new byte[MaxBodyBytes];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (n == 0) break;
            read += n;
        }

        var truncated = contentLength > MaxBodyBytes;
        var totalBytes = truncated ? contentLength : read;
        var body = System.Text.Encoding.UTF8.GetString(buffer, 0, read);

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        return (body, truncated, totalBytes);
    }
}
```

- [ ] **Step 4: Run all unit tests**

Run: `dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~AuthFailureAuditMiddlewareTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs tests/IngestionApi.Tests.Unit/Middleware/AuthFailureAuditMiddlewareTests.cs
git commit -m "feat(api): capture JSON body (8 KB cap) in 401 audit log"
```

---

## Task 5: Wire middleware into Program.cs

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs:34-36`

- [ ] **Step 1: Add the middleware registration**

Open `src/IngestionApi.Api/Program.cs`. Find:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
```

Replace with:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseMiddleware<AuthFailureAuditMiddleware>();
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build IngestionApi.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: All tests pass (existing + 8 new).

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Api/Program.cs
git commit -m "feat(api): register AuthFailureAuditMiddleware in pipeline"
```

---

## Task 6: Manual smoke check against VendorSignatureFilter (verification, no commit)

**Files:** none

- [ ] **Step 1: Run the API**

Run: `dotnet run --project src/IngestionApi.Api`

- [ ] **Step 2: Send a request with an unknown vendor id**

In another shell (PowerShell):

```powershell
Invoke-WebRequest -Uri https://localhost:7178/api/vendor-transactions/t `
  -Method POST `
  -Headers @{ "X-Vendor-Id" = "nope"; "X-Vendor-Signature" = "AAAA"; "Content-Type" = "application/json" } `
  -Body '{"hello":"world"}' `
  -SkipCertificateCheck
```

Expected: 401 response.

- [ ] **Step 3: Verify the audit line in logs**

Open the latest `src/IngestionApi.Api/logs/ingestion-*.log` file. Confirm a line containing `401 audit: POST /api/vendor-transactions/t` with `headers=` (vendor id visible, signature truncated) and `body={"hello":"world"}`.

- [ ] **Step 4: Stop the API. No commit needed.**

---

## Self-review

**Spec coverage:**
- Goals 1–4 (forensic record on every 401, mechanism-agnostic, bounded volume, Api-only) → Tasks 1, 2, 3, 4, 5.
- Pipeline placement after `RequestResponseLoggingMiddleware` and before `UseAuthorization()` → Task 5.
- Header redaction list (`Authorization`, `Cookie`, `Set-Cookie`, `Proxy-Authorization`, `X-Vendor-Signature` truncation) → Task 3.
- Body handling: empty / JSON small / JSON >8 KB / non-JSON → Task 4 (4 tests).
- `VendorSignatureFilter` and `RequestResponseLoggingMiddleware` untouched → confirmed (no edits in any task).
- Test plan items 1–5 (no log on 2xx, log on 401, redaction, truncation, empty body, non-JSON) covered. Item 7 (correlation id on event) is exercised by the existing `CorrelationIdMiddleware` running before this middleware; the `outputTemplate` already prints `{CorrelationId}` — no extra test needed since that pipe is verified by existing tests for `CorrelationIdMiddleware`.

**Placeholder scan:** none.

**Type/name consistency:** `AuthFailureAuditMiddleware`, `RedactHeaders`, `ReadBodyAsync`, `MaxBodyBytes`, `SensitiveHeaders`, `SignatureHeader`, `JsonContentTypePrefix`, log property names (`Method`, `Path`, `QueryString`, `RemoteIp`, `Headers`, `Body`, `BodyTruncated`, `BodyTotalBytes`) used consistently across all tasks.
