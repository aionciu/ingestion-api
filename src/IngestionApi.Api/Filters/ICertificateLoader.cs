using System.Security.Cryptography.X509Certificates;

namespace IngestionApi.Api.Filters;

public interface ICertificateLoader
{
    X509Certificate2? Load(string thumbprint);
}
