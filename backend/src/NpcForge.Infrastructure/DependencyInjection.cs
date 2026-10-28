using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Core.Campaigns;
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
        return services;
    }
}
