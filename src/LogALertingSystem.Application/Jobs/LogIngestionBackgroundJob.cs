using LogAlertingSystem.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogAlertingSystem.Application.Jobs;

public class LogIngestionBackgroundJob : BackgroundService
{
    private readonly ILogger<LogIngestionBackgroundJob> _logger;
    private readonly ILogIngestionService _logIngestionService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(Constants.BackgroundJobInterval);

    public LogIngestionBackgroundJob(
        ILogger<LogIngestionBackgroundJob> logger,
        ILogIngestionService logIngestionService,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _logIngestionService = logIngestionService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Log Ingestion Background Job started");

        try
        {
            _logger.LogInformation("Initializing log bookmarks");
            await _logIngestionService.InitializeBookmarksAsync();
            _logger.LogInformation("Log bookmarks initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing log bookmarks");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Checking new Windows Event Logs");

                var newLogs = await _logIngestionService.GetNewLogsAsync();

                if (newLogs.Any())
                {
                    _logger.LogInformation($"Found {newLogs.Count} new log entries");

                    using var scope = _serviceScopeFactory.CreateScope();
                    var logRepository = scope.ServiceProvider.GetRequiredService<ILogRepository>();
                    var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

                    await logRepository.AddRangeAsync(newLogs);
                    await logRepository.SaveChangesAsync();

                    var generatedAlerts = await alertService.EvaluateAndGenerateAlertsAsync(newLogs);

                    _logger.LogInformation($"Saved {newLogs.Count} logs, generated {generatedAlerts.Count} alerts");
                }

                await Task.Delay(_interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Log ingestion background job cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in log ingestion background job");
                await Task.Delay(_interval, cancellationToken);
            }
        }

        _logger.LogInformation("Log Ingestion Background Job stopped");
    }
}
