using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace LogAlertingSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ILogRepository, LogRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();

        return services;
    }
}
