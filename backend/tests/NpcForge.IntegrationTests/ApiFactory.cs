using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NpcForge.Core.Ai;
using NpcForge.Infrastructure.Persistence;
using NpcForge.IntegrationTests.Ai;
using Testcontainers.PostgreSql;

namespace NpcForge.IntegrationTests;

/// <summary>Sobe um Postgres real (Testcontainers) e a API em memória, compartilhados pelos testes.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public const string TestJwtKey = "test-only-jwt-key-with-at-least-32-characters";

    /// <summary>IA de mentira usada no lugar do Ollama (só existe com a IA ligada).</summary>
    public FakeAiProvider Fake => Services.GetRequiredService<FakeAiProvider>();

    protected virtual bool AiEnabled => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Ai:Enabled", AiEnabled.ToString());
        builder.UseSetting("Ai:TimeoutSeconds", "5");
        // Os testes do limite usam 5; o padrão do app é 0 (sem limite).
        builder.UseSetting("Ai:DailyGenerationLimit", "5");

        if (AiEnabled)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiProvider>();
                services.AddSingleton<FakeAiProvider>();
                services.AddSingleton<IAiProvider>(sp => sp.GetRequiredService<FakeAiProvider>());
            });
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
