using IngestionApi.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IngestionApi.Api.Filters;

public class VendorSignatureFilter : IAsyncResourceFilter
{
    private readonly VendorCertificateOptions _options;
    private readonly ICertificateLoader _certificateLoader;
    private readonly ILogger<VendorSignatureFilter> _logger;

    public VendorSignatureFilter(
        IOptions<VendorCertificateOptions> options,
        ICertificateLoader certificateLoader,
        ILogger<VendorSignatureFilter> logger)
    {
        _options = options.Value;
        _certificateLoader = certificateLoader;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var vendorId = request.Headers["X-Vendor-Id"].ToString();
        var signature = request.Headers["X-Vendor-Signature"].ToString();

        if (!_options.Vendors.TryGetValue(vendorId, out var entry))
        {
            _logger.LogWarning("Unknown vendor {VendorId}", vendorId);
            context.Result = new UnauthorizedObjectResult(new { detail = "Unknown vendor" });
            return;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Missing X-Vendor-Signature header for vendor {VendorId}", vendorId);
            context.Result = new UnauthorizedObjectResult(new { detail = "X-Vendor-Signature header is required" });
            return;
        }

        if (!entry.SignatureVerificationEnabled)
        {
            await next();
            return;
        }

        try
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
            var signatureBytes = Convert.FromBase64String(signature);

            var certificate = _certificateLoader.Load(entry.Thumbprint);
            if (certificate is null)
            {
                _logger.LogWarning("Certificate not found for vendor {VendorId} thumbprint {Thumbprint}", vendorId, entry.Thumbprint);
                context.Result = new UnauthorizedObjectResult(new { detail = "Invalid signature" });
                return;
            }

            using var rsa = certificate.GetRSAPublicKey()
                ?? throw new InvalidOperationException("Certificate has no RSA public key.");

            if (!rsa.VerifyData(bodyBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                _logger.LogWarning("Invalid signature for vendor {VendorId}", vendorId);
                context.Result = new UnauthorizedObjectResult(new { detail = "Invalid signature" });
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signature verification failed for vendor {VendorId}", vendorId);
            context.Result = new UnauthorizedObjectResult(new { detail = "Invalid signature" });
            return;
        }

        await next();
    }
}
