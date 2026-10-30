using System.Net;
using System.Text;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api-kestrel")]
public class NpcImportSizeLimitTests(KestrelApiFactory factory)
{
    [Fact]
    public async Task Import_FileOver20MB_Returns413()
    {
        // No teste no navegador, o Kestrel recusou o corpo grande, mas a resposta saiu como 500.
        var client = await factory.CreateAuthenticatedClientAsync();
        var json = $$"""{ "format": "npcforge-npcs", "version": 1, "npcs": [{ "name": "Grande", "notes": "{{new string('x', 21 * 1024 * 1024)}}" }] }""";

        // "Expect: 100-continue": o cliente espera o servidor aceitar antes de mandar o corpo. Sem isso, no Linux
        // o Kestrel responde 413 e fecha a conexão no meio do envio, e o teste via "Broken pipe" em vez do 413.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/npcs/import")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.ExpectContinue = true;

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
