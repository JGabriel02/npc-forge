using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NpcForge.IntegrationTests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_WithDatabaseUp_Returns200WithoutLogin()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}
