using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace NpcForge.IntegrationTests.Auth;

[Collection("api")]
public class AuthTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ValidData_Returns201()
    {
        var response = await RegisterAsync(UniqueEmail());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("joão-{0}@x.com")]
    [InlineData("o'neil-{0}@x.com")]
    public async Task Register_ValidEmailWithAccentOrApostrophe_Returns201AndCanLogin(string template)
    {
        var email = string.Format(template, Guid.NewGuid());

        var register = await RegisterAsync(email);
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = TestAuthExtensions.TestPassword });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmailDifferentCase_Returns400()
    {
        var id = Guid.NewGuid();
        await RegisterAsync($"joao-{id}@x.com");

        var response = await RegisterAsync($"Joao-{id}@x.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_SameEmailConcurrently_OneSucceedsOthersReturn400()
    {
        var email = UniqueEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => factory.CreateClient().PostAsJsonAsync("/api/auth/register",
                new { email, password = TestAuthExtensions.TestPassword, displayName = "Mestre Teste" })));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Single(statuses, HttpStatusCode.Created);
        Assert.All(statuses.Where(s => s != HttpStatusCode.Created), s => Assert.Equal(HttpStatusCode.BadRequest, s));
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        var response = await RegisterAsync(UniqueEmail(), password: "abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotEmpty(problem!.Errors);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndMeWorks()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = TestAuthExtensions.TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrEmpty(body!.Token));
        Assert.True(body.ExpiresAt > DateTime.UtcNow);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var me = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<MeBody>();
        Assert.Equal(email, meBody!.Email);
        Assert.Equal("Mestre Teste", meBody.DisplayName);
        Assert.False(string.IsNullOrEmpty(meBody.Id));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "errada123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksAccountEvenForCorrectPassword()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        for (var i = 0; i < 5; i++)
        {
            var wrong = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "errada123" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        var correct = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = TestAuthExtensions.TestPassword });

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
        Assert.Equal("application/problem+json", correct.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_SuccessfulLoginResetsFailedAttempts()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        for (var round = 0; round < 2; round++)
        {
            for (var i = 0; i < 4; i++)
                await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "errada123" });
            var ok = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = TestAuthExtensions.TestPassword });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = UniqueEmail(), password = "qualquer123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid()}@test.local";

    private Task<HttpResponseMessage> RegisterAsync(string email, string password = TestAuthExtensions.TestPassword)
        => _client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "Mestre Teste" });

    private record LoginBody(string Token, DateTime ExpiresAt);
    private record MeBody(string Id, string Email, string DisplayName);
}
