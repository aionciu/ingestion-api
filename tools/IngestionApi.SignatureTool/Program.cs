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
