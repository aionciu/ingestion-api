# Signature Tool Send Request Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `IngestionApi.SignatureTool` so it reads config from `appsettings.json`, signs the JSON file, and POSTs the signed request to the API, printing the HTTP status code and response body to stdout.

**Architecture:** Single `Program.cs` (top-level statements) is replaced entirely. Args shrink to `--file` (required) and `--config` (optional). Config is parsed with `System.Text.Json.JsonDocument`. Signing is unchanged. HTTP dispatch uses `System.Net.Http.HttpClient` with a bypass for localhost dev TLS. No external NuGet.

**Tech Stack:** .NET 10 console, `System.Text.Json`, `System.Net.Http`, `System.Security.Cryptography.X509Certificates`

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `tools/IngestionApi.SignatureTool/Program.cs` | Modify | Full rewrite: arg parsing, config loading, signing, HTTP POST, output |
| `src/IngestionApi.Api/appsettings.json` | Modify | Add `SignatureTool` section with `BaseUrl`, `VendorId`, `Endpoint` |

---

## Task 1: Add SignatureTool config to appsettings.json

**Files:**
- Modify: `src/IngestionApi.Api/appsettings.json`

- [ ] **Step 1: Add the SignatureTool section**

Current `appsettings.json` ends with `"VendorCertificates"` block. Add `SignatureTool` as a new top-level key. The full file should become:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=IngestionDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{CorrelationId}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/ingestion-.log",
          "rollingInterval": "Day",
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{CorrelationId}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": [ "FromLogContext" ]
  },
  "AllowedHosts": "*",
  "VendorCertificates": {
    "Vendors": {
      "vendor-001": {
        "Thumbprint": "64593ffc951060c0650fcdfbd417552f131d13bd",
        "SignatureVerificationEnabled": true
      }
    }
  },
  "SignatureTool": {
    "BaseUrl": "https://localhost:7178",
    "VendorId": "vendor-001",
    "Endpoint": "/api/vendor-transactions/t"
  }
}
```

- [ ] **Step 2: Verify the file is valid JSON**

```bash
cat src/IngestionApi.Api/appsettings.json | dotnet-script - 2>/dev/null || python3 -c "import json,sys; json.load(open('src/IngestionApi.Api/appsettings.json')); print('valid JSON')"
```

If python3 is not available, open the file and visually confirm brackets are balanced. No build step needed — this is just a config file.

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/appsettings.json
git commit -m "chore: add SignatureTool config section to appsettings.json"
```

---

## Task 2: Rewrite Program.cs

**Files:**
- Modify: `tools/IngestionApi.SignatureTool/Program.cs`

- [ ] **Step 1: Replace Program.cs with the full implementation**

```csharp
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

const string Usage = """
Usage: IngestionApi.SignatureTool --file <path> [--config <path>]

  --file     Path to the JSON file to sign and send
  --config   Path to appsettings.json (default: src/IngestionApi.Api/appsettings.json)

Prints HTTP status code and response body to stdout.
""";

string? filePath = null;
string? configPath = null;

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--file") filePath = args[i + 1];
    if (args[i] == "--config") configPath = args[i + 1];
}

if (filePath is null)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: file not found: {filePath}");
    return 1;
}

configPath ??= Path.Combine("src", "IngestionApi.Api", "appsettings.json");

if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Error: config not found: {configPath}");
    return 1;
}

using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
var root = doc.RootElement;

if (!root.TryGetProperty("SignatureTool", out var toolSection))
{
    Console.Error.WriteLine("Error: missing 'SignatureTool' section in config.");
    return 1;
}

if (!toolSection.TryGetProperty("BaseUrl", out var baseUrlEl) ||
    !toolSection.TryGetProperty("VendorId", out var vendorIdEl) ||
    !toolSection.TryGetProperty("Endpoint", out var endpointEl))
{
    Console.Error.WriteLine("Error: 'SignatureTool' must have BaseUrl, VendorId, and Endpoint.");
    return 1;
}

var baseUrl = baseUrlEl.GetString()!;
var vendorId = vendorIdEl.GetString()!;
var endpoint = endpointEl.GetString()!;

if (!root.TryGetProperty("VendorCertificates", out var vcSection) ||
    !vcSection.TryGetProperty("Vendors", out var vendorsSection) ||
    !vendorsSection.TryGetProperty(vendorId, out var vendorEntry) ||
    !vendorEntry.TryGetProperty("Thumbprint", out var thumbprintEl))
{
    Console.Error.WriteLine($"Error: thumbprint not found for vendor '{vendorId}' in config.");
    return 1;
}

var thumbprint = thumbprintEl.GetString()!;

using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
store.Open(OpenFlags.ReadOnly);
var results = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);

if (results.Count == 0)
{
    Console.Error.WriteLine($"Error: certificate not found for thumbprint: {thumbprint}");
    return 1;
}

var cert = results[0];
using var rsa = cert.GetRSAPrivateKey();

if (rsa is null)
{
    Console.Error.WriteLine("Error: certificate has no RSA private key.");
    return 1;
}

var bodyBytes = File.ReadAllBytes(filePath);
var signature = rsa.SignData(bodyBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var signatureBase64 = Convert.ToBase64String(signature);

using var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
using var client = new HttpClient(handler);

var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + endpoint);
request.Headers.Add("X-Vendor-Id", vendorId);
request.Headers.Add("X-Vendor-Signature", signatureBase64);
request.Content = new ByteArrayContent(bodyBytes);
request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

HttpResponseMessage response;
try
{
    response = await client.SendAsync(request);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: HTTP request failed: {ex.Message}");
    return 1;
}

var responseBody = await response.Content.ReadAsStringAsync();
Console.WriteLine((int)response.StatusCode);
Console.WriteLine(responseBody);
return 0;
```

- [ ] **Step 2: Build**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 3: Smoke test — missing --file arg**

```bash
dotnet run --project tools/IngestionApi.SignatureTool 2>&1; echo "exit: $?"
```

Expected: usage text on stderr, exit code 1.

- [ ] **Step 4: Smoke test — missing config**

```bash
dotnet run --project tools/IngestionApi.SignatureTool -- --file nonexistent.json --config bad.json 2>&1; echo "exit: $?"
```

Expected: `Error: file not found: nonexistent.json` (file check comes first), exit code 1.

- [ ] **Step 5: Smoke test — config not found**

```bash
echo '{}' > /tmp/test.json
dotnet run --project tools/IngestionApi.SignatureTool -- --file /tmp/test.json --config bad.json 2>&1; echo "exit: $?"
```

Expected: `Error: config not found: bad.json`, exit code 1.

- [ ] **Step 6: Commit**

```bash
git add tools/IngestionApi.SignatureTool/Program.cs
git commit -m "feat: extend signature tool to POST signed request"
```

---

## Task 3: End-to-end test with the running API

This task requires a real certificate in `LocalMachine\My` matching the thumbprint in `appsettings.json`, and the API running on `https://localhost:7178`.

- [ ] **Step 1: Confirm the cert exists**

```powershell
Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq "64593ffc951060c0650fcdfbd417552f131d13bd" }
```

If no output, install or create a dev cert first (see below).

**Creating a dev cert if needed:**

```powershell
$cert = New-SelfSignedCertificate -Subject "CN=IngestionApi-Dev" -CertStoreLocation "Cert:\LocalMachine\My" -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddYears(1)
Write-Host $cert.Thumbprint
```

Then update `appsettings.json` — set `VendorCertificates.Vendors.vendor-001.Thumbprint` to the new thumbprint.

- [ ] **Step 2: Start the API**

```bash
dotnet run --project src/IngestionApi.Api
```

Leave it running.

- [ ] **Step 3: Create a test body file**

```bash
echo '{"operationId":"op-001","bagId":"bag-001","bags":[{"declaredValue":100,"processedValue":0,"differenceValue":100,"currency":"USD"}]}' > /tmp/body.json
```

- [ ] **Step 4: Run the tool**

```bash
dotnet run --project tools/IngestionApi.SignatureTool -- --file /tmp/body.json
```

Expected: first line is an HTTP status code (e.g. `200` or `400`), second line is the response JSON body. No error output on stderr.
