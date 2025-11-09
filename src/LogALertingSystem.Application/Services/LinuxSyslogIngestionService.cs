using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace LogAlertingSystem.Application.Services;


public class LinuxSyslogIngestionService : ILogIngestionService
{
    private readonly ILogger<LinuxSyslogIngestionService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _syslogPath;
    private long _lastReadPosition = 0;
    private DateTime? _lastLogTimestamp = null;

    // Regex pattern for parsing syslog format
    // Example: Jan 15 10:30:45 hostname process[123]: message text
    private static readonly Regex SyslogPattern = new Regex(
        @"^(?<month>\w{3})\s+(?<day>\d{1,2})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<hostname>\S+)\s+(?<process>[^\[\s:]+)(?:\[(?<pid>\d+)\])?:\s+(?<message>.+)$",
        RegexOptions.Compiled);

    // Alternative pattern for systemd journal format
    // Example: 2024-01-15T10:30:45.123456+00:00 hostname process[123]: message
    private static readonly Regex SystemdPattern = new Regex(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+[+-]\d{2}:\d{2})\s+(?<hostname>\S+)\s+(?<process>[^\[\s:]+)(?:\[(?<pid>\d+)\])?:\s+(?<message>.+)$",
        RegexOptions.Compiled);

    public LinuxSyslogIngestionService(
        ILogger<LinuxSyslogIngestionService> logger,
        IServiceScopeFactory serviceScopeFactory,
        string syslogPath = "/var/log/syslog")
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _syslogPath = syslogPath;
    }

    public async Task InitializeBookmarksAsync()
    {
        try
        {
            _logger.LogInformation("Initializing syslog bookmarks");

            using var scope = _serviceScopeFactory.CreateScope();
            var logRepository = scope.ServiceProvider.GetRequiredService<ILogRepository>();

            var recentLogs = await logRepository.GetAllAsync(0, 1);

            if (recentLogs.Any())
            {
                _lastLogTimestamp = recentLogs[0].Timestamp;
                _logger.LogInformation($"Found existing logs in database. Starting from {_lastLogTimestamp}");
            }
            else
            {
                _lastLogTimestamp = DateTime.UtcNow.AddHours(-1);
                _logger.LogInformation("No existing logs in database. Starting from 1 hour ago");
            }

            // Initialize file position to end of file
            if (File.Exists(_syslogPath))
            {
                var fileInfo = new FileInfo(_syslogPath);
                _lastReadPosition = fileInfo.Length;
                _logger.LogInformation($"Initialized syslog position to {_lastReadPosition} bytes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing syslog bookmarks");
            _lastLogTimestamp = DateTime.UtcNow.AddHours(-1);
            _lastReadPosition = 0;
        }
    }

    public async Task<List<Log>> GetNewLogsAsync()
    {
        var logs = new List<Log>();

        try
        {
            if (!File.Exists(_syslogPath))
            {
                _logger.LogWarning("Syslog file not found at {Path}", _syslogPath);
                return logs;
            }

            var fileInfo = new FileInfo(_syslogPath);

            // Check if file was rotated (size decreased)
            if (fileInfo.Length < _lastReadPosition)
            {
                _logger.LogInformation("Log rotation detected. Restarting from beginning.");
                _lastReadPosition = 0;
            }

            // Read new lines from the last position
            using var fileStream = new FileStream(_syslogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(_lastReadPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(fileStream);
            string? line;
            int lineCount = 0;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineCount++;
                var log = ParseSyslogLine(line);

                if (log != null)
                {
                    // Only include logs after last timestamp
                    if (_lastLogTimestamp == null || log.Timestamp > _lastLogTimestamp)
                    {
                        logs.Add(log);
                    }
                }
            }

            // Update position
            _lastReadPosition = fileStream.Position;

            if (logs.Any())
            {
                _lastLogTimestamp = logs.Max(l => l.Timestamp);
                _logger.LogInformation($"Read {logs.Count} new syslog entries from {lineCount} lines");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading syslog from {_syslogPath}");
        }

        return logs;
    }

    private Log? ParseSyslogLine(string line)
    {
        try
        {
            // Try systemd format first
            var systemdMatch = SystemdPattern.Match(line);
            if (systemdMatch.Success)
            {
                return new Log
                {
                    Timestamp = DateTime.Parse(systemdMatch.Groups["timestamp"].Value).ToUniversalTime(),
                    Source = systemdMatch.Groups["process"].Value,
                    Type = "Syslog",
                    EventId = int.TryParse(systemdMatch.Groups["pid"].Value, out var pid) ? pid : null,
                    Level = InferLogLevel(systemdMatch.Groups["message"].Value),
                    Message = systemdMatch.Groups["message"].Value
                };
            }

            // Try traditional syslog format
            var syslogMatch = SyslogPattern.Match(line);
            if (syslogMatch.Success)
            {
                var timestamp = ParseSyslogTimestamp(
                    syslogMatch.Groups["month"].Value,
                    syslogMatch.Groups["day"].Value,
                    syslogMatch.Groups["time"].Value);

                return new Log
                {
                    Timestamp = timestamp,
                    Source = syslogMatch.Groups["process"].Value,
                    Type = "Syslog",
                    EventId = int.TryParse(syslogMatch.Groups["pid"].Value, out var pid) ? pid : null,
                    Level = InferLogLevel(syslogMatch.Groups["message"].Value),
                    Message = syslogMatch.Groups["message"].Value
                };
            }

            // If parsing fails, return null
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, $"Failed to parse syslog line: {line}");
            return null;
        }
    }

    private DateTime ParseSyslogTimestamp(string month, string day, string time)
    {
        // set up current year
        var currentYear = DateTime.UtcNow.Year;
        var monthNum = month switch
        {
            "Jan" => 1, "Feb" => 2, "Mar" => 3, "Apr" => 4,
            "May" => 5, "Jun" => 6, "Jul" => 7, "Aug" => 8,
            "Sep" => 9, "Oct" => 10, "Nov" => 11, "Dec" => 12,
            _ => 1
        };

        var dayNum = int.Parse(day.Trim());
        var timeParts = time.Split(':');
        var hour = int.Parse(timeParts[0]);
        var minute = int.Parse(timeParts[1]);
        var second = int.Parse(timeParts[2]);

        var timestamp = new DateTime(currentYear, monthNum, dayNum, hour, minute, second, DateTimeKind.Local);

        // if timestamp will be in future, it possible entry from last year
        if (timestamp > DateTime.Now)
        {
            timestamp = timestamp.AddYears(-1);
        }

        return timestamp.ToUniversalTime();
    }

    private EventLogLevel InferLogLevel(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        // Check for critical/fatal keywords
        if (lowerMessage.Contains("panic") ||
            lowerMessage.Contains("fatal") ||
            lowerMessage.Contains("critical") ||
            lowerMessage.Contains("segfault") ||
            lowerMessage.Contains("out of memory"))
        {
            return EventLogLevel.Critical;
        }

        // Check for error keywords
        if (lowerMessage.Contains("error") ||
            lowerMessage.Contains("fail") ||
            lowerMessage.Contains("exception") ||
            lowerMessage.Contains("denied"))
        {
            return EventLogLevel.Error;
        }

        // Check for warning keywords
        if (lowerMessage.Contains("warn") ||
            lowerMessage.Contains("deprecated") ||
            lowerMessage.Contains("timeout"))
        {
            return EventLogLevel.Warning;
        }

        return EventLogLevel.Information;
    }
}
