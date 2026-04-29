using IngestionApi.Api.Filters;
using IngestionApi.Infrastructure;
using IngestionApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngestionApi.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            var descriptorOptions = services.Where(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)).ToList();
            var descriptorDbContext = services.Where(
                d => d.ServiceType == typeof(DbContext) || d.ServiceType == typeof(AppDbContext)).ToList();

            foreach (var descriptor in descriptorOptions.Concat(descriptorDbContext))
            {
                services.Remove(descriptor);
            }

            // Remove all EF Core services (providers, pools, etc.)
            var efCoreServices = services.Where(
                d => d.ServiceType?.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                     d.ImplementationType?.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true).ToList();
            foreach (var descriptor in efCoreServices)
            {
                services.Remove(descriptor);
            }

            // Add DbContext with in-memory database
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Replace VendorSignatureFilter with a no-op so controller tests are not affected by signature verification
            var vendorFilterDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(VendorSignatureFilter));
            if (vendorFilterDescriptor is not null)
                services.Remove(vendorFilterDescriptor);
            services.AddScoped<VendorSignatureFilter, NoOpVendorSignatureFilter>();

            // Create database schema
            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Database.EnsureCreated();
            }
        });
    }

    private sealed class NoOpVendorSignatureFilter(
        Microsoft.Extensions.Options.IOptions<Api.Options.VendorCertificateOptions> options,
        ICertificateLoader loader,
        Microsoft.Extensions.Logging.ILogger<VendorSignatureFilter> logger)
        : VendorSignatureFilter(options, loader, logger)
    {
        public override Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
            => next();
    }
}
