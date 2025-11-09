using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LogAlertingSystem.Application.Services;

public class AlertService : IAlertService
{
    private readonly IAlertRuleRepository _alertRuleRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        IAlertRuleRepository alertRuleRepository,
        IAlertRepository alertRepository,
        ILogger<AlertService> logger)
    {
        _alertRuleRepository = alertRuleRepository;
        _alertRepository = alertRepository;
        _logger = logger;
    }

    public async Task<List<Alert>> EvaluateAndGenerateAlertsAsync(List<Log> logs)
    {
        if (!logs.Any())
        {
            return new List<Alert>();
        }

        // Get all active alert rules
        var activeRules = await _alertRuleRepository.GetActiveRulesAsync();

        if (!activeRules.Any())
        {
            _logger.LogInformation("No active alert rules found");
            return new List<Alert>();
        }

        var generatedAlerts = new List<Alert>();

        foreach (var log in logs)
        {
            foreach (var rule in activeRules)
            {
                if (EvaluateRule(rule, log))
                {
                    var alert = CreateAlert(rule, log);
                    generatedAlerts.Add(alert);

                    _logger.LogInformation(
                        "Alert generated: Rule '{RuleName}' matched log from {Source} at {Timestamp}",
                        rule.Name, log.Source, log.Timestamp);
                }
            }
        }

        // Save all generated alerts
        if (generatedAlerts.Any())
        {
            await _alertRepository.AddRangeAsync(generatedAlerts);
            await _alertRepository.SaveChangesAsync();

            _logger.LogInformation("Saved {Count} alerts to database", generatedAlerts.Count);
        }

        return generatedAlerts;
    }

    private bool EvaluateRule(AlertRule rule, Log log)
    {
        return CheckContainsCondition(log.Message, rule.MessageContainsCondition)
            || CheckEqualsCondition(log.Message, rule.MessageEqualCondition)
            || CheckContainsCondition(log.Source, rule.SourceContainsCondition)
            || CheckEqualsCondition(log.Source, rule.SourceEqualCondition)
            || CheckContainsCondition(log.Type, rule.TypeContainsCondition)
            || CheckEqualsCondition(log.Type, rule.TypeEqualCondition)
            || CheckLogLevelCondition(log.Level, rule.LogLevel);
    }

    private bool CheckLogLevelCondition(EventLogLevel logLevel, EventLogLevel? ruleLogLevel)
    {
        // If rule has no LogLevel condition, it doesn't match
        if (!ruleLogLevel.HasValue)
        {
            return false;
        }

        return logLevel == ruleLogLevel.Value;
    }

    private bool CheckContainsCondition(string value, string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return false;
        }

        return value.Contains(condition, StringComparison.OrdinalIgnoreCase);
    }

    private bool CheckEqualsCondition(string value, string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return false;
        }

        return value.Equals(condition, StringComparison.OrdinalIgnoreCase);
    }

    private Alert CreateAlert(AlertRule rule, Log log)
    {
        return new Alert
        {
            CreatedAt = DateTime.UtcNow,
            AlertRuleId = rule.Id,
            AlertRule = rule,
            LogId = log.Id,
            Log = log,
            Title = $"Alert: {rule.Name}",
            Message = $"Log message: {log.Message.Substring(0, Math.Min(200, log.Message.Length))}..."
        };
    }
}
