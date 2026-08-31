using System.Net;
using System.Net.Http.Json;
using EmojiWaterfall.Api.IntegrationTests.Infrastructure;

namespace EmojiWaterfall.Api.IntegrationTests.Endpoints;

public sealed class HealthEndpointTests : IClassFixture<EmojiWaterfallWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(EmojiWaterfallWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Should_Report_Ok()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal("ok", health?.Status);
    }

    private sealed record HealthResponse(string Status);
}
