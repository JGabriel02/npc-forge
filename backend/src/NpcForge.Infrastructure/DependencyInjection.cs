using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Core.Ai;
using NpcForge.Core.Campaigns;
using NpcForge.Core.Demo;
using NpcForge.Core.LocalUser;
using NpcForge.Core.Npcs;
using NpcForge.Infrastructure.Ai;
using NpcForge.Infrastructure.Demo;
using NpcForge.Infrastructure.LocalUser;
using NpcForge.Infrastructure.Persistence;
using NpcForge.Infrastructure.Services;

namespace NpcForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<INpcService, NpcService>();
        services.AddScoped<INpcTransferService, NpcTransferService>();
        services.AddOptions<DemoOptions>().BindConfiguration(DemoOptions.Section);
        services.AddScoped<DemoSeeder>();
        services.AddOptions<LocalUserOptions>().BindConfiguration(LocalUserOptions.Section);
        services.AddScoped<LocalUserSeeder>();

        services.AddAi(configuration);
        return services;
    }

    // Com a IA desligada (versão web), nada de IA é registrado nem exige configuração.
    private static void AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var options = services.AddOptions<AiOptions>().BindConfiguration(AiOptions.Section);
        var ai = configuration.GetSection(AiOptions.Section).Get<AiOptions>() ?? new AiOptions();
        if (!ai.Enabled) return;

        options.ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OllamaOptions>()
            .BindConfiguration($"{AiOptions.Section}:{nameof(AiOptions.Ollama)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<PromptBuilder>();
        services.AddScoped<IAiUsageService, AiUsageService>();
        services.AddScoped<INpcGenerationService, NpcGenerationService>();
        services.AddHttpClient<IAiProvider, OllamaProvider>(http =>
        {
            http.BaseAddress = new Uri(ai.Ollama.BaseUrl);
            // O limite de tempo é controlado pelo NpcGenerationService (Ai:TimeoutSeconds).
            http.Timeout = Timeout.InfiniteTimeSpan;
        });
    }
}
