namespace IngestionApi.Api.Options;

public class VendorCertificateOptions
{
    public Dictionary<string, VendorCertificateEntry> Vendors { get; set; } = new();
}

public class VendorCertificateEntry
{
    public string Thumbprint { get; set; } = string.Empty;
    public bool SignatureVerificationEnabled { get; set; }
}
