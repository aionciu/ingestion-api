# Vendor Signature Validation Design

**Date:** 2026-04-29

## Goal

Validate an `X-Vendor-Signature` header on all `VendorTransactionsController` endpoints. The signature is an RSA-SHA256 signature over the raw JSON request body, computed by the vendor using their private key. We verify using the vendor's public certificate, which we issue and manage.

## Request Contract

Every request to `POST /api/vendor-transactions/*` must include:

| Header | Description |
|---|---|
| `X-Vendor-Id` | Vendor identifier (already required) |
| `X-Vendor-Signature` | Base64-encoded RSA-SHA256 signature of the raw UTF-8 request body |

## Certificate Configuration

Certificates are looked up by thumbprint from the Windows Certificate Store. Configuration is stored in `appsettings.json` under `VendorCertificates`, keyed by vendor ID:

```json
"VendorCertificates": {
  "vendor-001": {
    "Thumbprint": "A1B2C3D4E5F6...",
    "SignatureVerificationEnabled": true
  },
  "vendor-002": {
    "Thumbprint": "B2C3D4E5F6A1...",
    "SignatureVerificationEnabled": false
  }
}
```

`SignatureVerificationEnabled: false` allows a vendor to bypass signature validation (e.g. during onboarding). An unknown vendor (not present in config) always returns `401`.

> **Future migration note:** Certificate loading is abstracted behind `ICertificateLoader`. When migrating to Linux, only `WindowsCertificateLoader` needs to be replaced.

## Components

### New Files

| File | Responsibility |
|---|---|
| `src/IngestionApi.Api/Options/VendorCertificateOptions.cs` | Strongly-typed config bound from `VendorCertificates` section. `Dictionary<string, VendorCertificateEntry>` where entry has `Thumbprint` and `SignatureVerificationEnabled`. |
| `src/IngestionApi.Api/Filters/ICertificateLoader.cs` | Interface: `X509Certificate2? Load(string thumbprint)` |
| `src/IngestionApi.Api/Filters/WindowsCertificateLoader.cs` | Implements `ICertificateLoader` using `X509Store(StoreName.My, StoreLocation.LocalMachine)` |
| `src/IngestionApi.Api/Filters/VendorSignatureFilter.cs` | `IAsyncActionFilter` — validates `X-Vendor-Signature` against vendor's certificate |

### Modified Files

| File | Change |
|---|---|
| `src/IngestionApi.Api/Program.cs` | Register `ICertificateLoader`, bind `VendorCertificateOptions`, register `VendorSignatureFilter` |
| `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs` | Apply `[ServiceFilter(typeof(VendorSignatureFilter))]` at class level |

## Filter Logic

1. Read `X-Vendor-Id` and `X-Vendor-Signature` headers
2. If `X-Vendor-Signature` is missing → `401 {"detail": "X-Vendor-Signature header is required"}`
3. Look up vendor in `VendorCertificateOptions`; if not found → `401 {"detail": "Unknown vendor"}`
4. If `SignatureVerificationEnabled: false` → skip to next filter (pass-through)
5. Load certificate via `ICertificateLoader` using the vendor's thumbprint
6. Enable request body buffering; read raw UTF-8 body bytes
7. Base64-decode the signature header value
8. Verify RSA-SHA256 signature; on failure or exception → `401 {"detail": "Invalid signature"}`
9. Rewind body stream so the controller can read it normally

## Error Responses

All rejections return `401 Unauthorized`:

| Scenario | Response body |
|---|---|
| Missing signature header | `{"detail": "X-Vendor-Signature header is required"}` |
| Unknown vendor | `{"detail": "Unknown vendor"}` |
| Invalid/tampered signature | `{"detail": "Invalid signature"}` |

Each rejection logs a `Warning` with vendor ID and reason. Exceptions during verification are caught and logged — no internal details leaked.

## Testing

### Unit Tests (`IngestionApi.Tests.Unit`)

Test `VendorSignatureFilter` directly with mocked `ICertificateLoader` and `VendorCertificateOptions`:
- Missing `X-Vendor-Signature` header → 401
- Unknown vendor → 401
- `SignatureVerificationEnabled: false` → pass-through
- Valid RSA-SHA256 signature → pass
- Invalid signature → 401
- Certificate not found in store → 401

### Integration Tests (`IngestionApi.Tests.Integration`)

Use `WebApplicationFactory` with a real RSA key pair generated in-test. Register a stub `ICertificateLoader` returning the test certificate:
- Correctly signed request → `202 Accepted`
- Tampered body → `401`
