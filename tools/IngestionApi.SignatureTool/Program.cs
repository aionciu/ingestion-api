using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

const string Usage = """
Usage: IngestionApi.SignatureTool --thumbprint <thumbprint> --file <path>

  --thumbprint   Certificate thumbprint in the LocalMachine\My store
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
