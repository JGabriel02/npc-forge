using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;
using NpcForge.Infrastructure.Ai;

namespace NpcForge.UnitTests.Ai;

public class OllamaProviderTests
{
    private const string Schema = """{ "type": "object", "properties": { "name": { "type": "string" } } }""";

    private static (OllamaProvider Provider, FakeHandler Handler) Create(Func<HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = Options.Create(new AiOptions { Ollama = new OllamaOptions { Model = "qwen2.5:14b" } });
        return (new OllamaProvider(http, options), handler);
    }

    private static HttpResponseMessage Ok(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new { model = "qwen2.5:14b", message = new { role = "assistant", content }, done = true }),
            Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SendsModelSchemaAndMessagesWithStreamFalse()
    {
        var (provider, handler) = Create(() => Ok("{}"));

        await provider.CompleteJsonAsync("sistema", "usuário", Schema, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/chat", handler.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("qwen2.5:14b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("format").ValueKind);
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(["system", "user"], messages.Select(m => m.GetProperty("role").GetString()));
        Assert.Equal("usuário", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ReturnsMessageContent()
    {
        var (provider, _) = Create(() => Ok("""{ "name": "Borin" }"""));

        var content = await provider.CompleteJsonAsync("s", "u", Schema, CancellationToken.None);

        Assert.Equal("""{ "name": "Borin" }""", content);
    }

    [Fact]
    public async Task ConnectionRefused_ThrowsAiProviderExceptionInPortuguese()
    {
        var (provider, _) = Create(() => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.CompleteJsonAsync("s", "u", Schema, CancellationToken.None));

        Assert.Equal("A IA não está disponível agora.", ex.Message);
    }

    [Fact]
    public async Task ModelNotFound_ThrowsAiProviderExceptionMentioningPull()
    {
        var (provider, _) = Create(() => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"model \"qwen2.5:14b\" not found, try pulling it first"}"""),
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.CompleteJsonAsync("s", "u", Schema, CancellationToken.None));

        Assert.Contains("ollama pull qwen2.5:14b", ex.Message);
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        var (provider, _) = Create(() => Ok("{}"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteJsonAsync("s", "u", Schema, cts.Token));
    }

    private sealed class FakeHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond();
        }
    }
}
