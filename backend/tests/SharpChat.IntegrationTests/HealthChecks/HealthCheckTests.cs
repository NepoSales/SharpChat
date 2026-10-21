using System.Net;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.HealthChecks;

[Collection(ApiTestGroup.Name)]
public sealed class HealthCheckTests(SharpChatApiFactory factory)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
