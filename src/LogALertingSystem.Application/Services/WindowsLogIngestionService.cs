using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;

namespace LogAlertingSystem.Application.Services;

public class WindowsLogIngestionService : ILogIngestionService
{
    private readonly ILogger<WindowsLogIngestionService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string[] _logNames = { "Application", "System", "Security" };
    private readonly Dictionary<string, EventBookmark?> _bookmarks = new();

    public WindowsLogIngestionService(ILogger<WindowsLogIngestionService> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;

        foreach (var logName in _logNames)
        {
            _bookmarks[logName] = null;
        }
    }

    public async Task InitializeBookmarksAsync()
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logRepository = scope.ServiceProvider.GetRequiredService<ILogRepository>();

            var recentLogs = await logRepository.GetAllAsync(0, 1);
            DateTime startTime;

            if (recentLogs.Any())
            {
                // If we have any logs in db, we will require to start from latest one to check new ones
                startTime = recentLogs[0].Timestamp;
                _logger.LogInformation("Start read log from - {StartTime}", startTime.ToLocalTime());
            }
            else
            {
                // If no logs, we will start from today midnigth
                startTime = DateTime.Today.ToUniversalTime();
                _logger.LogInformation("Start read logs from midnigth.");
            }

            foreach (var logName in _logNames)
            {
                _bookmarks[logName] = GetBookmarkFromTimestamp(logName, startTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something going wrongs with initializing bookmarks.");
        }
    }

    public async Task<List<Log>> GetNewLogsAsync()
    {
        var allLogs = new List<Log>();

        // Application, system, security logs
        foreach (var logName in _logNames)
        {
            try
            {
                var logs = await ReadNewEventsAsync(logName);
                if (logs.Any())
                {
                    allLogs.AddRange(logs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading new events from {logName}");
            }
        }

        return allLogs;
    }

    private async Task<List<Log>> ReadNewEventsAsync(string logName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var query = new EventLogQuery(logName, PathType.LogName)
                {
                    ReverseDirection = false
                };

                using var reader = new EventLogReader(query, _bookmarks[logName]);
                var logs = new List<Log>();
                EventRecord? eventRecord;
                EventBookmark? lastBookmark = null;

                while ((eventRecord = reader.ReadEvent()) != null)
                {
                    using (eventRecord)
                    {
                        var log = ConvertEventRecordToLog(eventRecord, logName);
                        if (log != null)
                        {
                            logs.Add(log);
                        }
                        lastBookmark = eventRecord.Bookmark;
                    }

                    if (logs.Count >= Constants.LimitOfLogToProcess)
                    {
                        break;
                    }
                }

                if (lastBookmark != null)
                {
                    _bookmarks[logName] = lastBookmark;
                }

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading new events from {logName}");
                return new List<Log>();
            }
        });
    }

    private Log? ConvertEventRecordToLog(EventRecord eventRecord, string logName)
    {
        try
        {
            return new Log
            {
                Timestamp = eventRecord.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                EventId = eventRecord.Id,
                Level = MapEventLevel(eventRecord.Level),
                Source = eventRecord.ProviderName ?? logName,
                Type = GetEventType(eventRecord),
                Message = GetEventMessage(eventRecord)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert event record {EventId} from {LogName}",
                eventRecord?.Id, logName);
            return null;
        }
    }

    private Domain.Enums.EventLogLevel MapEventLevel(byte? level)
    {
        return level switch
        {
            1 => Domain.Enums.EventLogLevel.Critical,
            2 => Domain.Enums.EventLogLevel.Error,
            3 => Domain.Enums.EventLogLevel.Warning,
            4 => Domain.Enums.EventLogLevel.Information,
            // LogAlways
            0 => Domain.Enums.EventLogLevel.Information,
            _ => Domain.Enums.EventLogLevel.Information
        };
    }

    private string GetEventType(EventRecord eventRecord)
    {
        try
        {
            string taskName = eventRecord.TaskDisplayName;
            if (!string.IsNullOrWhiteSpace(taskName))
            {
                return taskName;
            }

            return eventRecord.LevelDisplayName ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetEventMessage(EventRecord eventRecord)
    {
        try
        {
            var description = eventRecord.FormatDescription();
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            var properties = eventRecord.Properties
                .Select(p => p.Value?.ToString() ?? string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            return string.Join(" | ", properties);
        }
        catch (Exception)
        {
            return $"Event ID: {eventRecord.Id}, Provider: {eventRecord.ProviderName}";
        }
    }

    private EventBookmark? GetBookmarkFromTimestamp(string logName, DateTime startTime)
    {
        try
        {
            var startTimeUtc = startTime.ToUniversalTime();

            var startTimeString = startTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var queryString = $@"
                <QueryList>
                    <Query Id='0' Path='{logName}'>
                        <Select Path='{logName}'>
                            *[System[TimeCreated[@SystemTime &gt;= '{startTimeString}']]]
                        </Select>
                    </Query>
                </QueryList>";

            var query = new EventLogQuery(logName, PathType.LogName, queryString);
            query.ReverseDirection = false;

            using var reader = new EventLogReader(query);
            using var eventRecord = reader.ReadEvent();

            if (eventRecord != null)
            {
                var bookmark = eventRecord.Bookmark;
                _logger.LogInformation($"Set bookmark for {logName} to start from {startTime.ToLocalTime()}");
                return bookmark;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Could not set bookmark from timestamp for {logName}, will start from current position");
            return null;
        }
    }
}
