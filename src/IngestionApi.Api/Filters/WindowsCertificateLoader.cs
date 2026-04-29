using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;

namespace IngestionApi.Api.Filters;

[ExcludeFromCodeCoverage]
public class WindowsCertificateLoader : ICertificateLoader
{
    public X509Certificate2? Load(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var results = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        for (int i = 1; i < results.Count; i++) results[i].Dispose();
        return results.Count > 0 ? results[0] : null;
    }
}
