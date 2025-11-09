using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Application.Jobs;
using LogAlertingSystem.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogAlertingSystem.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ILogIngestionService, WindowsLogIngestionService>();
        services.AddHostedService<LogIngestionBackgroundJob>();

        services.AddScoped<IAlertService, AlertService>();

        return services;
    }
}
