using LogAlertingSystem.Domain.Enums;

namespace LogAlertingSystem.Domain.Entities;

public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; }

    // Types of conditions
    public string MessageContainsCondition { get; set; }
    public string MessageEqualCondition { get; set; }

    public string SourceContainsCondition { get; set; }
    public string SourceEqualCondition { get; set; }

    public string TypeContainsCondition { get; set; }
    public string TypeEqualCondition { get; set; }
    
    public EventLogLevel? LogLevel { get; set; }
    public bool IsActive { get; set; }
}
