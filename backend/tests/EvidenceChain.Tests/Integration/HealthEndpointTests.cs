using System.Net;
using EvidenceChain.Tests.Integration.Api;

namespace EvidenceChain.Tests.Integration;

[Collection(ApiCollection.Name)]
public class HealthEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
