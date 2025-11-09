using LogAlertingSystem.Domain.Enums;

namespace LogAlertingSystem.Domain.Entities;

public class Alert
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public int AlertRuleId { get; set; }
    public AlertRule AlertRule { get; set; } = null!;

    public int LogId { get; set; }
    public Log Log { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
