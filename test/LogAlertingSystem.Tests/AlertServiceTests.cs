using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Application.Services;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LogAlertingSystem.Tests;

public class AlertServiceTests
{
    private readonly Mock<IAlertRuleRepository> _mockAlertRuleRepository;
    private readonly Mock<IAlertRepository> _mockAlertRepository;
    private readonly Mock<ILogger<AlertService>> _mockLogger;
    private readonly AlertService _alertService;

    public AlertServiceTests()
    {
        _mockAlertRuleRepository = new Mock<IAlertRuleRepository>();
        _mockAlertRepository = new Mock<IAlertRepository>();
        _mockLogger = new Mock<ILogger<AlertService>>();

        _alertService = new AlertService(
            _mockAlertRuleRepository.Object,
            _mockAlertRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_WithNoLogs_ReturnsEmptyList()
    {
        // Arrange
        var logs = new List<Log>();

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(logs);

        // Assert
        Assert.Empty(result);
        _mockAlertRuleRepository.Verify(x => x.GetActiveRulesAsync(), Times.Never);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_WithNoActiveRules_ReturnsEmptyList()
    {
        // Arrange
        var logs = new List<Log>
        {
            new Log
            {
                Id = 1,
                Level = EventLogLevel.Error,
                Message = "Test error",
                Source = "TestSource",
                Type = "Error",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule>());

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(logs);

        // Assert
        Assert.Empty(result);
        _mockAlertRepository.Verify(x => x.AddRangeAsync(It.IsAny<List<Alert>>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_MessageContainsCondition_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Error,
            Message = "Database connection failed",
            Source = "TestSource",
            Type = "Error",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Database Error Alert",
            MessageContainsCondition = "connection failed",
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
        Assert.Equal("Alert: Database Error Alert", result[0].Title);
        Assert.Equal(log.Id, result[0].LogId);
        Assert.Equal(rule.Id, result[0].AlertRuleId);

        _mockAlertRepository.Verify(x => x.AddRangeAsync(It.Is<List<Alert>>(a => a.Count == 1)), Times.Once);
        _mockAlertRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_MessageEqualsCondition_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Warning,
            Message = "Disk space low",
            Source = "SystemMonitor",
            Type = "Warning",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Disk Space Alert",
            MessageEqualCondition = "Disk space low",
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
        _mockAlertRepository.Verify(x => x.AddRangeAsync(It.IsAny<List<Alert>>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_SourceContainsCondition_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Error,
            Message = "Service crashed",
            Source = "MyApplication.Service",
            Type = "Error",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Service Error Alert",
            SourceContainsCondition = "MyApplication",
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_LogLevelCondition_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Critical,
            Message = "System failure",
            Source = "System",
            Type = "Critical",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Critical Alert",
            LogLevel = EventLogLevel.Critical,
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_NullLogLevel_DoesNotMatch()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Error,
            Message = "Error message",
            Source = "System",
            Type = "Error",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Any Level Alert",
            LogLevel = null, // No level condition
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_MultipleConditions_AnyMatches_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Error,
            Message = "Database timeout",
            Source = "WebAPI",
            Type = "Error",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Database Alert",
            MessageContainsCondition = "timeout",
            SourceContainsCondition = "NonMatchingSource",
            LogLevel = EventLogLevel.Warning, // Different level
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_NoConditionsMatch_DoesNotGenerateAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Information,
            Message = "Normal operation",
            Source = "Application",
            Type = "Info",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Error Alert",
            MessageContainsCondition = "error",
            LogLevel = EventLogLevel.Error,
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Empty(result);
        _mockAlertRepository.Verify(x => x.AddRangeAsync(It.IsAny<List<Alert>>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_MultipleRules_GeneratesMultipleAlerts()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Error,
            Message = "Critical database error",
            Source = "Database",
            Type = "Error",
            Timestamp = DateTime.UtcNow
        };

        var rule1 = new AlertRule
        {
            Id = 1,
            Name = "Database Alert",
            SourceContainsCondition = "Database",
            IsActive = true
        };

        var rule2 = new AlertRule
        {
            Id = 2,
            Name = "Error Level Alert",
            LogLevel = EventLogLevel.Error,
            IsActive = true
        };

        var rule3 = new AlertRule
        {
            Id = 3,
            Name = "Critical Message Alert",
            MessageContainsCondition = "Critical",
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule1, rule2, rule3 });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Equal(3, result.Count);
        _mockAlertRepository.Verify(x => x.AddRangeAsync(It.Is<List<Alert>>(a => a.Count == 3)), Times.Once);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_CaseInsensitiveMatching_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Warning,
            Message = "ERROR OCCURRED",
            Source = "Application",
            Type = "Warning",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Error Detection",
            MessageContainsCondition = "error occurred",
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task EvaluateAndGenerateAlertsAsync_TypeConditions_GeneratesAlert()
    {
        // Arrange
        var log = new Log
        {
            Id = 1,
            Level = EventLogLevel.Error,
            Message = "Test message",
            Source = "Application",
            Type = "SecurityException",
            Timestamp = DateTime.UtcNow
        };

        var rule = new AlertRule
        {
            Id = 1,
            Name = "Security Alert",
            TypeContainsCondition = "Security",
            IsActive = true
        };

        _mockAlertRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<AlertRule> { rule });

        // Act
        var result = await _alertService.EvaluateAndGenerateAlertsAsync(new List<Log> { log });

        // Assert
        Assert.Single(result);
    }
}
