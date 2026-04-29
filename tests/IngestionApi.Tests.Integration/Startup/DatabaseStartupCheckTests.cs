using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IngestionApi.Tests.Integration.Startup;

public class DatabaseStartupCheckTests
{
    [Fact]
    public async Task App_ThrowsOnStartup_WhenDatabaseIsUnreachable()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            "Server=invalid-host-that-does-not-exist;Database=test;Trusted_Connection=True;Connect Timeout=2;"
                    });
                });
            });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Task.FromResult(factory.CreateClient()));
    }
}
