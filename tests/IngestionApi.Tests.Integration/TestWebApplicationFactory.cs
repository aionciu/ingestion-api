using IngestionApi.Infrastructure;
using IngestionApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
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

            // Create database schema
            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Database.EnsureCreated();
            }
        });
    }
}
