using System.Net;
using System.Text;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api-kestrel")]
public class NpcImportSizeLimitTests(KestrelApiFactory factory)
{
    [Fact]
    public async Task Import_FileOver5MB_Returns413()
    {
        // No teste no navegador, o Kestrel recusou o corpo grande, mas a resposta saiu como 500.
        var client = await factory.CreateAuthenticatedClientAsync();
        var json = $$"""{ "format": "npcforge-npcs", "version": 1, "npcs": [{ "name": "Grande", "notes": "{{new string('x', 6 * 1024 * 1024)}}" }] }""";

        var response = await client.PostAsync("/api/npcs/import", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
