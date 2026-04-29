# Signature Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 10 console tool that loads a certificate from the Windows Certificate Store by thumbprint, signs a JSON file with RSA-SHA256 PKCS#1, and prints the base64-encoded signature to stdout.

**Architecture:** Single `Program.cs` using top-level statements. No external NuGet dependencies — only BCL (`System.Security.Cryptography.X509Certificates`, `System.Security.Cryptography`). Arguments parsed manually from `args[]`.

**Tech Stack:** .NET 10 console app, `System.Security.Cryptography.X509Certificates`, `System.Security.Cryptography`

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `tools/IngestionApi.SignatureTool/IngestionApi.SignatureTool.csproj` | Create | .NET 10 console project file |
| `tools/IngestionApi.SignatureTool/Program.cs` | Create | All logic: arg parsing, cert loading, signing, output |
| `IngestionApi.slnx` | Modify | Add `tools/` solution folder with the new project |

---

## Task 1: Create the console project

**Files:**
- Create: `tools/IngestionApi.SignatureTool/IngestionApi.SignatureTool.csproj`
- Modify: `IngestionApi.slnx`

- [ ] **Step 1: Create the project file**

Create `tools/IngestionApi.SignatureTool/IngestionApi.SignatureTool.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Create a placeholder Program.cs so the project builds**

Create `tools/IngestionApi.SignatureTool/Program.cs`:

```csharp
Console.WriteLine("Signature Tool");
```

- [ ] **Step 3: Add the project to the solution**

Edit `IngestionApi.slnx` to add a `tools/` folder entry. The full file should become:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/IngestionApi.Api/IngestionApi.Api.csproj" />
    <Project Path="src/IngestionApi.Application/IngestionApi.Application.csproj" />
    <Project Path="src/IngestionApi.Domain/IngestionApi.Domain.csproj" />
    <Project Path="src/IngestionApi.Infrastructure/IngestionApi.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/IngestionApi.Tests.Unit/IngestionApi.Tests.Unit.csproj" />
    <Project Path="tests/IngestionApi.Tests.Integration/IngestionApi.Tests.Integration.csproj" />
  </Folder>
  <Folder Name="/tools/">
    <Project Path="tools/IngestionApi.SignatureTool/IngestionApi.SignatureTool.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 4: Build to verify the project compiles**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add tools/IngestionApi.SignatureTool/IngestionApi.SignatureTool.csproj tools/IngestionApi.SignatureTool/Program.cs IngestionApi.slnx
git commit -m "chore: scaffold IngestionApi.SignatureTool console project"
```

---

## Task 2: Implement Program.cs

**Files:**
- Modify: `tools/IngestionApi.SignatureTool/Program.cs`

- [ ] **Step 1: Replace Program.cs with the full implementation**

```csharp
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

const string Usage = """
Usage: IngestionApi.SignatureTool --thumbprint <thumbprint> --file <path>

  --thumbprint   Certificate thumbprint in the LocalMachine\\My store
  --file         Path to the JSON file to sign

Prints the base64-encoded RSA-SHA256 signature to stdout.
""";

string? thumbprint = null;
string? filePath = null;

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--thumbprint") thumbprint = args[i + 1];
    if (args[i] == "--file") filePath = args[i + 1];
}

if (thumbprint is null || filePath is null)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: file not found: {filePath}");
    return 1;
}

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
Console.WriteLine(Convert.ToBase64String(signature));
return 0;
```

- [ ] **Step 2: Build**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 3: Smoke test — missing arguments**

```bash
dotnet run --project tools/IngestionApi.SignatureTool
```

Expected: usage text printed to stderr, process exits with code 1. You won't see the exit code directly but the usage text confirms the path works.

- [ ] **Step 4: Smoke test — file not found**

```bash
dotnet run --project tools/IngestionApi.SignatureTool -- --thumbprint AABBCC --file nonexistent.json
```

Expected: `Error: file not found: nonexistent.json` printed to stderr.

- [ ] **Step 5: Smoke test — cert not found**

Create a temporary test file:
```bash
echo '{"test":1}' > /tmp/test-body.json
dotnet run --project tools/IngestionApi.SignatureTool -- --thumbprint INVALID-THUMB --file /tmp/test-body.json
```

Expected: `Error: certificate not found for thumbprint: INVALID-THUMB` printed to stderr.

- [ ] **Step 6: Commit**

```bash
git add tools/IngestionApi.SignatureTool/Program.cs
git commit -m "feat: implement signature tool"
```

---

## Task 3: Verify end-to-end with a real certificate

This task requires a real certificate with a private key in `LocalMachine\My`. Skip if no cert is available in the test environment.

- [ ] **Step 1: Find a certificate thumbprint**

```powershell
Get-ChildItem Cert:\LocalMachine\My | Select-Object Thumbprint, Subject
```

Copy a thumbprint from the output.

- [ ] **Step 2: Create a test JSON body file**

```bash
echo '{"operationId":"op-001","bagId":"bag-001","bags":[{"declaredValue":100,"processedValue":0,"differenceValue":100,"currency":"USD"}]}' > /tmp/body.json
```

- [ ] **Step 3: Run the tool**

```bash
dotnet run --project tools/IngestionApi.SignatureTool -- --thumbprint <your-thumbprint> --file /tmp/body.json
```

Expected: a base64 string printed to stdout (no newline issues, no error output).

- [ ] **Step 4: Verify the signature validates**

The output can be used directly as the `X-Vendor-Signature` header value when sending a request to the API with the matching vendor ID.
