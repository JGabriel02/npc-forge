using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NpcForge.IntegrationTests.Auth;

// Cada teste usa um IP próprio no X-Forwarded-For, para os limites de um não afetarem o outro.
[Collection("api-rate-limited")]
public class RateLimitTests(RateLimitedApiFactory factory)
{
    private static readonly object WrongLogin = new { email = "x@test.local", password = "errada123" };

    [Fact]
    public async Task Login_OverLimit_Returns429WithMessage()
    {
        var client = ClientFrom("203.0.113.10");
        for (var i = 0; i < 3; i++) await client.PostAsJsonAsync("/api/auth/login", WrongLogin);

        var response = await client.PostAsJsonAsync("/api/auth/login", WrongLogin);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("Muitas tentativas. Aguarde um minuto e tente de novo.",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task DifferentForwardedIps_HaveSeparateLimits()
    {
        var first = ClientFrom("203.0.113.20");
        for (var i = 0; i < 3; i++) await first.PostAsJsonAsync("/api/auth/login", WrongLogin);

        var response = await ClientFrom("203.0.113.21").PostAsJsonAsync("/api/auth/login", WrongLogin);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task SpoofedLeftmostForwardedIp_DoesNotEscapeLimit()
    {
        // O cliente pode mandar o próprio X-Forwarded-For; o proxy da Azure acrescenta o IP real no fim.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
            last = await ClientFrom($"198.51.100.{i}, 203.0.113.30").PostAsJsonAsync("/api/auth/login", WrongLogin);

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [Fact]
    public async Task Register_IsAlsoLimited()
    {
        var client = ClientFrom("203.0.113.40");
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
            last = await client.PostAsJsonAsync("/api/auth/register",
                new { email = $"{Guid.NewGuid()}@test.local", password = "senha1234", displayName = "Mestre" });

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    private HttpClient ClientFrom(string forwardedFor)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }
}
