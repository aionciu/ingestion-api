# Signature Tool Design

**Date:** 2026-04-29
**Branch:** `feature/vendor-signature`

## Goal

A developer console tool for generating `X-Vendor-Signature` header values. Used internally to test signed requests against the ingestion API.

## Usage

```bash
dotnet run --project tools/IngestionApi.SignatureTool -- --thumbprint <thumbprint> --file <path-to-body.json>
```

Prints the base64-encoded RSA-SHA256 signature to stdout. Ready to copy into the `X-Vendor-Signature` header.

## Project Structure

```
tools/
  IngestionApi.SignatureTool/
    IngestionApi.SignatureTool.csproj   (.NET 10 console, no external dependencies)
    Program.cs
```

Added to `IngestionApi.slnx` under a new `tools/` solution folder.

## Behaviour

1. Parse `--thumbprint` and `--file` arguments
2. If either is missing → print usage to stderr, exit code 1
3. Read file as raw UTF-8 bytes
4. Open Windows Certificate Store (`StoreName.My`, `StoreLocation.LocalMachine`), find cert by thumbprint
5. If cert not found → print error to stderr, exit code 1
6. Extract RSA private key from cert; if no private key → print error to stderr, exit code 1
7. Sign bytes with RSA-SHA256 PKCS#1
8. Print base64-encoded signature to stdout, exit code 0

## Error Handling

All errors go to stderr so the signature output on stdout is always clean and pipeable. Exit codes: `0` = success, `1` = any failure.
