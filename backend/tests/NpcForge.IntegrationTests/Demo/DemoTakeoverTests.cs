using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Infrastructure.Demo;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.IntegrationTests.Demo;

// A revisão final achou: alguém podia cadastrar o e-mail da demo com senha e controlar o que os visitantes veem.
[Collection("api")]
public class DemoEmailReservedTests(ApiFactory factory)
{
    [Theory]
    [InlineData("visitante@npcforge.demo")]
    [InlineData("VISITANTE@NPCFORGE.DEMO")]
    [InlineData("outro@npcforge.demo")]
    public async Task Register_DemoDomain_IsRejected(string email)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email, password = TestAuthExtensions.TestPassword, displayName = "Invasor" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()),
            m => m.GetString() == "Este e-mail é reservado.");
    }
}

[Collection("api-demo-takeover")]
public class DemoTakeoverTests(DemoTakeoverApiFactory factory)
{
    [Fact]
    public async Task DemoLogin_WhenDemoEmailHasAPassword_RefusesInsteadOfUsingIt()
    {
        // Uma conta com senha no e-mail da demo (criada por fora da API) não pode virar a conta dos visitantes.
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await users.CreateAsync(
                new AppUser { UserName = DemoSeeder.Email, Email = DemoSeeder.Email, DisplayName = "Invasor" },
                TestAuthExtensions.TestPassword);
        }

        var response = await factory.CreateClient().PostAsync("/api/auth/demo", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
