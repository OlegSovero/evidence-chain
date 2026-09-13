using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace EvidenceChain.Tests.Integration;

public class HealthEndpointTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _sqlContainer.StartAsync();

    public Task DisposeAsync() => _sqlContainer.DisposeAsync().AsTask();

    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Sql"] = _sqlContainer.GetConnectionString(),
                    })));

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
