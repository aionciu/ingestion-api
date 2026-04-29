# Signature Tool — Send Request Design

**Date:** 2026-04-29
**Branch:** `feature/vendor-signature`

## Goal

Extend `IngestionApi.SignatureTool` to sign a JSON file and send the signed POST request to the ingestion API, printing the HTTP status code and response body to stdout for manual testing.

## Usage

```bash
dotnet run --project tools/IngestionApi.SignatureTool -- --file body.json
# optional override:
dotnet run --project tools/IngestionApi.SignatureTool -- --file body.json --config path/to/appsettings.json
```

Prints to stdout:
```
200
{"result":"ok"}
```

## Config

Add a `SignatureTool` section to `src/IngestionApi.Api/appsettings.json`:

```json
"SignatureTool": {
  "BaseUrl": "https://localhost:7178",
  "VendorId": "vendor-001",
  "Endpoint": "/api/vendor-transactions/t"
}
```

The thumbprint is read from `VendorCertificates.Vendors[VendorId].Thumbprint` — no duplication.

Default config path (when `--config` is not passed): walk up from the tool's working directory to find `src/IngestionApi.Api/appsettings.json`. When run from repo root this resolves to `src/IngestionApi.Api/appsettings.json`.

## Behaviour

1. Parse `--file` (required) and optional `--config <path>`
2. Load `appsettings.json` via `System.Text.Json.JsonDocument`
3. Read `SignatureTool.BaseUrl`, `SignatureTool.VendorId`, `SignatureTool.Endpoint`
4. Read thumbprint from `VendorCertificates.Vendors[VendorId].Thumbprint`
5. Load cert from `LocalMachine\My`, extract RSA private key
6. Sign file bytes with RSA-SHA256 PKCS#1, base64-encode
7. POST `{BaseUrl}{Endpoint}` with:
   - `Content-Type: application/json`
   - `X-Vendor-Id: {VendorId}`
   - `X-Vendor-Signature: {base64signature}`
   - Body: raw file bytes
8. Print `{status code}\n{response body}` to stdout
9. Exit 0 always — caller interprets the status code

## Error Handling

All errors (missing file, missing config keys, cert not found, no private key, HTTP transport error) go to stderr. Exit code 1 on any failure before the HTTP call. If the HTTP call itself succeeds (any status), exit 0 and print the result.

## Implementation Notes

- No external NuGet — `System.Net.Http.HttpClient` and `System.Text.Json` are BCL
- `HttpClient` configured with `ServerCertificateCustomValidationCallback` to accept the dev HTTPS cert (localhost self-signed)
- Single `Program.cs`, top-level statements
