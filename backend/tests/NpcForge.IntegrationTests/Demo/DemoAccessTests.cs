using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NpcForge.IntegrationTests.Demo;

[Collection("api")]
public class DemoAccessTests(ApiFactory factory)
{
    private const string ReadOnly = "A conta de visitante é só para leitura. Crie uma conta para salvar seus NPCs.";

    [Fact]
    public async Task DemoLogin_ReturnsTokenForTheDemoAccount()
    {
        var client = await factory.CreateDemoClientAsync();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.Equal("visitante@npcforge.demo", me.GetProperty("email").GetString());
        Assert.True(me.GetProperty("isDemo").GetBoolean());
    }

    [Fact]
    public async Task RegularUser_IsNotDemo()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.False(me.GetProperty("isDemo").GetBoolean());
    }

    [Theory]
    [InlineData("POST", "/api/npcs")]
    [InlineData("PUT", "/api/npcs/{npc}")]
    [InlineData("DELETE", "/api/npcs/{npc}")]
    [InlineData("PUT", "/api/npcs/{npc}/statblock")]
    [InlineData("DELETE", "/api/npcs/{npc}/statblock")]
    [InlineData("POST", "/api/npcs/import")]
    [InlineData("POST", "/api/campaigns")]
    [InlineData("PUT", "/api/campaigns/{campaign}")]
    [InlineData("DELETE", "/api/campaigns/{campaign}")]
    public async Task Demo_CannotWrite(string method, string route)
    {
        var client = await factory.CreateDemoClientAsync();
        var npcs = await client.GetFromJsonAsync<JsonElement>("/api/npcs?pageSize=100");
        var campaigns = await client.GetFromJsonAsync<JsonElement>("/api/campaigns");
        var url = route
            .Replace("{npc}", npcs.GetProperty("items")[0].GetProperty("id").GetString())
            .Replace("{campaign}", campaigns[0].GetProperty("id").GetString());

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ReadOnly, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        Assert.Equal(8, (await client.GetFromJsonAsync<JsonElement>("/api/npcs")).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("/api/npcs")]
    [InlineData("/api/campaigns")]
    [InlineData("/api/npcs/export")]
    public async Task Demo_CanRead(string route)
    {
        var client = await factory.CreateDemoClientAsync();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Demo_CanExportOneNpc()
    {
        var client = await factory.CreateDemoClientAsync();
        var npcs = await client.GetFromJsonAsync<JsonElement>("/api/npcs");

        var response = await client.GetAsync($"/api/npcs/{npcs.GetProperty("items")[0].GetProperty("id").GetString()}/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegularUser_CanStillWrite()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/npcs", new { name = "Borin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

[Collection("api-demo-disabled")]
public class DemoDisabledTests(DemoDisabledApiFactory factory)
{
    [Fact]
    public async Task DemoLogin_WhenDisabled_Returns404()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/demo", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
