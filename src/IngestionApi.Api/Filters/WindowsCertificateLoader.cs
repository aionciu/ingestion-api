using System.Security.Cryptography.X509Certificates;

namespace IngestionApi.Api.Filters;

public class WindowsCertificateLoader : ICertificateLoader
{
    public X509Certificate2? Load(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        var results = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        return results.Count > 0 ? results[0] : null;
    }
}
