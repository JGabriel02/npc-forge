using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NpcForge.IntegrationTests;

public static class TestAuthExtensions
{
    public const string TestPassword = "senha1234";

    /// <summary>Registra um usuário novo (e-mail único), faz login e devolve um client autenticado.</summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.local";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = TestPassword, displayName = "Mestre Teste" });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestPassword });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }


    /// <summary>Entra como visitante (conta demo somente leitura) e devolve um client autenticado.</summary>
    public static async Task<HttpClient> CreateDemoClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsync("/api/auth/demo", null);
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private record LoginBody(string Token, DateTime ExpiresAt);
}
