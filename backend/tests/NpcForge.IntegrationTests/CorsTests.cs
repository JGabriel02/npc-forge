namespace NpcForge.IntegrationTests;

[Collection("api")]
public class CorsTests(ApiFactory factory)
{
    [Fact]
    public async Task Preflight_FromAllowedOrigin_ReturnsAllowOriginHeader()
    {
        var response = await PreflightAsync("http://localhost:4200");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal(["http://localhost:4200"], values);
    }

    [Fact]
    public async Task Preflight_FromOtherOrigin_HasNoAllowOriginHeader()
    {
        var response = await PreflightAsync("http://evil.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/campaigns");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return factory.CreateClient().SendAsync(request);
    }
}
