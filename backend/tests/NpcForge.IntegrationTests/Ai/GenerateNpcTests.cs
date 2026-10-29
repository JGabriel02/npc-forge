using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NpcForge.Core.Ai;

namespace NpcForge.IntegrationTests.Ai;

[Collection("api")]
public class GenerateNpcTests : IDisposable
{
    private readonly ApiFactory _factory;

    public GenerateNpcTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Fake.Reset();
    }

    public void Dispose() => _factory.Fake.Reset();

    [Fact]
    public async Task Generate_ReturnsDraftWarningsAndUsage()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "costureira estranha", withStatBlock = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GenerateBody>(Json);
        Assert.Equal("Vasilka", body!.Draft.Name);
        Assert.Equal("4d8 + 4", body.Draft.StatBlock!.HitDice);
        Assert.Empty(body.Warnings);
        Assert.Equal(1, body.Usage.Used);
        Assert.Equal(5, body.Usage.Limit);
    }

    [Fact]
    public async Task Generate_SendsPromptAndCampaignSettingToTheModel()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var campaign = await client.PostAsJsonAsync("/api/campaigns", new { name = "Strahd", setting = "terror gótico em Barovia" });
        var campaignId = (await campaign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await GenerateAsync(client, new { prompt = "costureira estranha", campaignId, withStatBlock = false });

        var call = Assert.Single(_factory.Fake.Calls);
        Assert.Contains("costureira estranha", call.User);
        Assert.Contains("terror gótico em Barovia", call.User);
    }

    [Fact]
    public async Task Generate_WithOtherUsersCampaign_Returns400AndDoesNotConsume()
    {
        var other = await _factory.CreateAuthenticatedClientAsync();
        var campaign = await other.PostAsJsonAsync("/api/campaigns", new { name = "Alheia" });
        var campaignId = (await campaign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", campaignId, withStatBlock = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await UsageAsync(client)).Used);
        Assert.Empty(_factory.Fake.Calls);
    }

    [Fact]
    public async Task Generate_FixesBadValuesWithWarnings()
    {
        _factory.Fake.Enqueue(FakeAiProvider.DefaultJson.Replace("\"str\": 14", "\"str\": 35"));
        var client = await _factory.CreateAuthenticatedClientAsync();

        var body = await (await GenerateAsync(client, new { prompt = "x", withStatBlock = true }))
            .Content.ReadFromJsonAsync<GenerateBody>(Json);

        Assert.Equal(30, body!.Draft.StatBlock!.Str);
        Assert.Contains(body.Warnings, w => w.Contains("Força"));
    }

    [Fact]
    public async Task Generate_RetriesOnceOnInvalidJson()
    {
        _factory.Fake.Enqueue("Desculpe, não consigo.");
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _factory.Fake.Calls.Count);
        Assert.Contains("não pôde ser usada", _factory.Fake.Calls.Last().User);
    }

    [Fact]
    public async Task Generate_TwoInvalidResponses_Returns502AndDoesNotConsume()
    {
        _factory.Fake.Enqueue("lixo");
        _factory.Fake.Enqueue("mais lixo");
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = true });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, (await UsageAsync(client)).Used);
    }

    [Fact]
    public async Task Generate_AiUnavailable_Returns502InPortugueseAndDoesNotConsume()
    {
        _factory.Fake.EnqueueFailure(new AiProviderException("A IA não está disponível agora."));
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = false });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("A IA não está disponível agora.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        Assert.Equal(0, (await UsageAsync(client)).Used);
    }

    [Fact]
    public async Task Generate_UnexpectedProviderError_Returns502AndDoesNotConsume()
    {
        // Rede de segurança: nenhuma saída estranha do provedor pode virar erro 500.
        _factory.Fake.EnqueueFailure(new InvalidOperationException("algo inesperado"));
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = false });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, (await UsageAsync(client)).Used);
    }

    [Fact]
    public async Task Generate_SlowModel_Returns504AndDoesNotConsume()
    {
        _factory.Fake.EnqueueDelay(TimeSpan.FromSeconds(10));
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = false });

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(0, (await UsageAsync(client)).Used);
    }

    [Fact]
    public async Task Generate_RequestedChallengeRatingWins()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var body = await (await GenerateAsync(client, new { prompt = "x", withStatBlock = true, challengeRating = "2" }))
            .Content.ReadFromJsonAsync<GenerateBody>(Json);

        Assert.Equal("2", body!.Draft.StatBlock!.ChallengeRating);
        Assert.Contains("Nível de Desafio 2", _factory.Fake.Calls.Single().User);
    }

    [Fact]
    public async Task Generate_WithoutStatBlock_DropsModelStatBlock()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var body = await (await GenerateAsync(client, new { prompt = "x", withStatBlock = false }))
            .Content.ReadFromJsonAsync<GenerateBody>(Json);

        Assert.Null(body!.Draft.StatBlock);
    }

    [Fact]
    public async Task Generate_SixthOfTheDay_Returns429WithResetsAt()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await GenerateAsync(client, new { prompt = "x", withStatBlock = false })).StatusCode);

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = false });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Você atingiu o limite de gerações de hoje.", problem.GetProperty("title").GetString());
        Assert.True(problem.GetProperty("resetsAt").GetDateTime() > DateTime.UtcNow);
        Assert.Equal(5, _factory.Fake.Calls.Count);
    }

    [Fact]
    public async Task Generate_ConcurrentRequests_ExactlyLimitSucceed()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => GenerateAsync(client, new { prompt = "x", withStatBlock = false })));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Generate_EmptyPrompt_Returns400(string? prompt)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt, withStatBlock = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_InvalidChallengeRating_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await GenerateAsync(client, new { prompt = "x", withStatBlock = true, challengeRating = "1/3" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_WithoutToken_Returns401()
    {
        var response = await GenerateAsync(_factory.CreateClient(), new { prompt = "x", withStatBlock = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Usage_ForNewUser_IsZeroWithResetsAt()
    {
        var usage = await UsageAsync(await _factory.CreateAuthenticatedClientAsync());

        Assert.Equal(0, usage.Used);
        Assert.Equal(5, usage.Limit);
        Assert.True(usage.ResetsAt > DateTime.UtcNow);
    }

    private static Task<HttpResponseMessage> GenerateAsync(HttpClient client, object body)
        => client.PostAsJsonAsync("/api/ai/generate-npc", body);

    private static async Task<AiUsageDto> UsageAsync(HttpClient client)
        => (await client.GetFromJsonAsync<AiUsageDto>("/api/ai/usage"))!;

    // A API manda enums como texto ("Medium"); o teste precisa ler do mesmo jeito.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private record GenerateBody(NpcDraft Draft, List<string> Warnings, AiUsageDto Usage);
}
