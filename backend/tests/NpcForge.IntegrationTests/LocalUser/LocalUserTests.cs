using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Infrastructure.Identity;
using NpcForge.Infrastructure.LocalUser;

namespace NpcForge.IntegrationTests.LocalUser;

[Collection("api")]
public class LocalUserTests(ApiFactory factory)
{
    [Fact]
    public async Task EnsureCreated_TwiceInARow_CreatesOneEmptyAccount()
    {
        await Seed();
        await Seed();

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(LocalUserSeeder.Email);
        Assert.NotNull(user);
        Assert.Equal("Mestre", user.DisplayName);
    }

    [Fact]
    public async Task LocalUser_CanLogInWithTheKnownPassword_AndStartsEmpty()
    {
        await Seed();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = LocalUserSeeder.Email, password = LocalUserSeeder.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var npcs = await client.GetFromJsonAsync<JsonElement>("/api/npcs");
        Assert.Equal(0, npcs.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Features_ReportsLocalUser()
    {
        var features = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/features");

        Assert.True(features.GetProperty("localUser").GetBoolean());
    }

    private async Task Seed()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LocalUserSeeder>().EnsureCreatedAsync();
    }
}

public class LocalUserGuardTests
{
    [Fact]
    public void LocalUser_InProduction_RefusesToStart()
    {
        // Senha conhecida publicada no README: nunca pode valer num site no ar.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Jwt:Key", ApiFactory.TestJwtKey);
            b.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=nao-usado");
            b.UseSetting("LocalUser:Enabled", "true");
        });

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains("LocalUser:Enabled", error.ToString());
    }
}
