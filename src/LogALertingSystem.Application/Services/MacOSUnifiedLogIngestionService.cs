using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace LogAlertingSystem.Application.Services;

public class MacOSUnifiedLogIngestionService : ILogIngestionService
{
    private readonly ILogger<MacOSUnifiedLogIngestionService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private DateTime? _lastLogTimestamp = null;
    private readonly string _logCommand = "log";

    public MacOSUnifiedLogIngestionService(
        ILogger<MacOSUnifiedLogIngestionService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InitializeBookmarksAsync()
    {
        try
        {
            _logger.LogInformation("Initializing macOS Unified Log bookmarks");

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing macOS log bookmarks");
            _lastLogTimestamp = DateTime.UtcNow.AddHours(-1);
        }
    }

    public async Task<List<Log>> GetNewLogsAsync()
    {
        var logs = new List<Log>();

        try
        {
            // Check if running on macOS
            if (!OperatingSystem.IsMacOS())
            {
                _logger.LogWarning("Not running on macOS. Unified Log ingestion is not available.");
                return logs;
            }

            // Calculate time range
            var startTime = _lastLogTimestamp ?? DateTime.UtcNow.AddMinutes(-5);
            var timeRange = CalculateTimeRangeArgument(startTime);

            // Execute log show command
            var logEntries = await ExecuteLogShowCommandAsync(timeRange);

            foreach (var entry in logEntries)
            {
                var log = ParseMacOSLogEntry(entry);
                if (log != null && (log.Timestamp > startTime))
                {
                    logs.Add(log);
                }
            }

            if (logs.Any())
            {
                _lastLogTimestamp = logs.Max(l => l.Timestamp);
                _logger.LogInformation($"Retrieved {logs.Count} new macOS log entries");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading macOS Unified Logs");
        }

        return logs;
    }

    private string CalculateTimeRangeArgument(DateTime startTime)
    {
        var now = DateTime.UtcNow;
        var timeSpan = now - startTime;

        if (timeSpan.TotalMinutes < 60)
        {
            return $"{(int)Math.Ceiling(timeSpan.TotalMinutes)}m";
        }
        else if (timeSpan.TotalHours < 24)
        {
            return $"{(int)Math.Ceiling(timeSpan.TotalHours)}h";
        }
        else
        {
            return $"{(int)Math.Ceiling(timeSpan.TotalDays)}d";
        }
    }

    private async Task<List<MacOSLogEntry>> ExecuteLogShowCommandAsync(string timeRange)
    {
        var entries = new List<MacOSLogEntry>();

        try
        {
            // Use JSON format for easier parsing
            // Command: log show --style json --predicate 'eventType == logEvent' --last <timeRange>
            var arguments = $"show --style json --predicate \"eventType == logEvent\" --last {timeRange}";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _logCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError($"log command failed with exit code {process.ExitCode}. Error: {error}");
                return entries;
            }

            // Parse JSON output
            if (!string.IsNullOrWhiteSpace(output))
            {
                entries = ParseJsonLogOutput(output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing log show command");
        }

        return entries;
    }

    private List<MacOSLogEntry> ParseJsonLogOutput(string jsonOutput)
    {
        var entries = new List<MacOSLogEntry>();

        try
        {
            // macOS log show --style json outputs an array of log entries
            using var document = JsonDocument.Parse(jsonOutput);

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    try
                    {
                        var entry = new MacOSLogEntry
                        {
                            Timestamp = element.TryGetProperty("timestamp", out var ts)
                                ? DateTime.Parse(ts.GetString()!)
                                : DateTime.UtcNow,
                            Process = element.TryGetProperty("process", out var proc)
                                ? proc.GetString() ?? "Unknown"
                                : "Unknown",
                            ProcessId = element.TryGetProperty("processID", out var pid)
                                ? pid.GetInt32()
                                : 0,
                            EventType = element.TryGetProperty("eventType", out var et)
                                ? et.GetString() ?? "logEvent"
                                : "logEvent",
                            MessageType = element.TryGetProperty("messageType", out var mt)
                                ? mt.GetString() ?? "Default"
                                : "Default",
                            Message = element.TryGetProperty("eventMessage", out var msg)
                                ? msg.GetString() ?? string.Empty
                                : string.Empty,
                            Category = element.TryGetProperty("category", out var cat)
                                ? cat.GetString() ?? string.Empty
                                : string.Empty,
                            Subsystem = element.TryGetProperty("subsystem", out var sub)
                                ? sub.GetString() ?? string.Empty
                                : string.Empty
                        };

                        entries.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to parse individual log entry");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JSON log output");
        }

        return entries;
    }

    private Log? ParseMacOSLogEntry(MacOSLogEntry entry)
    {
        try
        {
            return new Log
            {
                Timestamp = entry.Timestamp.ToUniversalTime(),
                Source = !string.IsNullOrWhiteSpace(entry.Process) ? entry.Process : "macOS",
                Type = !string.IsNullOrWhiteSpace(entry.Category)
                    ? entry.Category
                    : entry.EventType,
                EventId = entry.ProcessId != 0 ? entry.ProcessId : null,
                Level = MapMessageTypeToLogLevel(entry.MessageType),
                Message = FormatLogMessage(entry)
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse macOS log entry");
            return null;
        }
    }

    private string FormatLogMessage(MacOSLogEntry entry)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(entry.Subsystem))
        {
            parts.Add($"[{entry.Subsystem}]");
        }

        if (!string.IsNullOrWhiteSpace(entry.Category) && entry.Category != entry.Subsystem)
        {
            parts.Add($"[{entry.Category}]");
        }

        parts.Add(entry.Message);

        return string.Join(" ", parts);
    }

    private EventLogLevel MapMessageTypeToLogLevel(string messageType)
    {
        return messageType.ToLowerInvariant() switch
        {
            "fault" => EventLogLevel.Critical,
            "error" => EventLogLevel.Error,
            "warning" => EventLogLevel.Warning,
            "info" => EventLogLevel.Information,
            "debug" => EventLogLevel.Information,
            "default" => EventLogLevel.Information,
            _ => EventLogLevel.Information
        };
    }

    /// macOS Unified Log entry
    private class MacOSLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Process { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Subsystem { get; set; } = string.Empty;
    }
}
