# 401 Forensic Audit Logging — Design

Date: 2026-04-29

## Problem

When a request is rejected with HTTP 401 (today produced exclusively by `VendorSignatureFilter`), the existing log output is sparse: `RequestResponseLoggingMiddleware` records only method, path, status, and elapsed ms; the filter itself adds a one-line `LogWarning` describing *which* check failed. There is not enough information in the logs to forensically diagnose why a vendor's call was rejected (was the body what we expected? what headers did they send? what was the signature value? what source IP?).

We want, on every 401, a structured log entry capturing the full request envelope.

## Goals

- On any HTTP 401 response, emit one structured warning containing method, path, query string, source IP, correlation id, all request headers (with redaction), and the request body.
- Cover every 401 — current `VendorSignatureFilter` paths and any future auth mechanism — without per-mechanism code changes.
- Bound log volume (header redaction, body size cap).
- Keep the change isolated to the Api layer.

## Non-goals

- Logging full request on non-401 responses.
- Logging response bodies.
- Persisting audit records to a separate store. Standard Serilog sinks (console + rolling file) are sufficient.
- Auth scheme changes.

## Design

### New component

`AuthFailureAuditMiddleware` in `src/IngestionApi.Api/Middleware/AuthFailureAuditMiddleware.cs`.

Single responsibility: when the downstream pipeline produces a 401, emit a structured forensic log record.

### Pipeline placement

In `Program.cs`, registered immediately after `RequestResponseLoggingMiddleware` and before `UseAuthorization()`:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseMiddleware<AuthFailureAuditMiddleware>(); // new
...
app.UseAuthorization();
```

This ordering ensures `CorrelationId` is already enriched on the log context, exceptions thrown by downstream code are still handled, and the middleware sits outside the MVC filter pipeline so it observes the final 401 status set by `VendorSignatureFilter` (or any future auth component).

### Algorithm

1. On entry, call `context.Request.EnableBuffering()` so the body stream can be rewound after the downstream pipeline has consumed it.
2. `await _next(context)`.
3. If `context.Response.StatusCode != StatusCodes.Status401Unauthorized`, return — no audit work.
4. Otherwise, build the audit record:
   - `Method` — `context.Request.Method`
   - `Path` — `context.Request.Path.Value`
   - `QueryString` — `context.Request.QueryString.Value`
   - `RemoteIp` — `context.Connection.RemoteIpAddress?.ToString()`
   - `Headers` — dictionary produced by the redaction helper (see below)
   - `Body`, `BodyTruncated`, `BodyTotalBytes` — produced by the body-read helper (see below)
5. Emit one Serilog warning using a message template that lets every property be searchable as a structured field:

   ```csharp
   _logger.LogWarning(
       "401 audit: {Method} {Path}{QueryString} from {RemoteIp} headers={@Headers} body={Body} truncated={BodyTruncated} totalBytes={BodyTotalBytes}",
       method, path, queryString, remoteIp, headers, body, bodyTruncated, bodyTotalBytes);
   ```

### Header redaction

A private static helper `RedactHeaders(IHeaderDictionary)` returns `Dictionary<string, string>`:

- Case-insensitive matches on header name.
- `Authorization`, `Cookie`, `Set-Cookie`, `Proxy-Authorization` → value replaced with `***`.
- `X-Vendor-Signature` → first 8 characters + `…(len=N)` (a base64 RSA signature is large and contributes no diagnostic value beyond a prefix used to correlate with what the vendor sent).
- All other headers → joined with `, ` if multi-valued, otherwise the raw string.

### Body handling

A private helper `ReadBodyAsync(HttpRequest)` returning `(string Body, bool Truncated, long TotalBytes)`:

- If `Content-Length` is null or 0, return `("", false, 0)`.
- If `Content-Type` does not start with `application/json` (case-insensitive), skip the read and return `("[non-json content omitted]", false, contentLength)`. The API only accepts JSON; this guard exists so a future binary endpoint cannot accidentally dump bytes into the log.
- Otherwise, rewind `request.Body` to position 0, read up to `MaxBodyBytes = 8192` UTF-8 bytes into a buffer.
- If the underlying stream still has more data after 8192 bytes, set `Truncated = true` and `TotalBytes = Content-Length`; otherwise `Truncated = false` and `TotalBytes = bytesRead`.
- Always rewind the stream back to position 0 before returning (defensive — although nothing downstream will read it again, this preserves the invariant set by `EnableBuffering`).

### Interaction with existing code

- `VendorSignatureFilter` is **unchanged**. Its existing one-line `LogWarning` calls remain — they describe *why* auth failed and complement the forensic record (which describes *what* the request was).
- `RequestResponseLoggingMiddleware` is **unchanged**.
- `CorrelationIdMiddleware` runs earlier, so `CorrelationId` is already on the log context and will appear in the emitted line via the existing Serilog `outputTemplate`.

### Configuration

No new configuration. Constants (max body bytes, redacted header names) live as `private const` / `private static readonly` in the middleware class. If we ever want to tune them per environment we can extract an `AuthAuditOptions`, but YAGNI for now.

## SOLID / Clean Architecture review

- **SRP** — the middleware does one thing: emit a forensic record on 401.
- **OCP** — adding a new auth scheme (JWT, API key, etc.) requires no change here; any 401 it produces will be captured.
- **LSP / ISP** — n/a (no inheritance / interface surface introduced).
- **DIP** — depends only on `RequestDelegate`, `HttpContext`, `ILogger<AuthFailureAuditMiddleware>`.
- **Clean Architecture** — purely Api-layer concern. No reference from Application or Domain.

## Testing

Unit/integration tests live alongside existing middleware tests (location to be confirmed when writing the plan: `tests/IngestionApi.Tests.Unit` or `tests/IngestionApi.Tests.Integration`, matching where `RequestResponseLoggingMiddleware` is currently tested).

Cases:

1. 2xx response → middleware emits no audit record.
2. 401 response from `VendorSignatureFilter` (unknown vendor, missing signature, invalid signature, certificate not found) → exactly one audit record per request with expected fields populated.
3. Sensitive headers (`Authorization`, `Cookie`, `X-Vendor-Signature`) are redacted; non-sensitive headers pass through verbatim.
4. Body larger than 8 KB → `BodyTruncated = true`, `BodyTotalBytes` reflects `Content-Length`, `Body` is exactly 8192 bytes.
5. Empty body / `Content-Length: 0` → `Body = ""`, `BodyTruncated = false`, `BodyTotalBytes = 0`.
6. Non-JSON `Content-Type` → body replaced with `[non-json content omitted]`, headers still logged.
7. Correlation id is present on the emitted log event (verified via Serilog test sink).

## Risks

- **Log volume** — capped by 8 KB body limit and header redaction; impact is bounded.
- **PII / secrets in body** — JSON bodies for vendor transactions are not expected to contain credentials, but operators should be aware that 401 audit records contain raw payload data. Documented in the file's class-level comment.
- **Buffering cost on 2xx requests** — `EnableBuffering` is invoked unconditionally. For JSON-sized payloads this is negligible; if profile data later shows a hot spot we can short-circuit when there's no body.

## Out of scope / future work

- Externalizing the audit channel (separate log sink, SIEM forwarder).
- Configuration knobs for redaction lists and size cap.
- Logging request body on 403 / 5xx.
