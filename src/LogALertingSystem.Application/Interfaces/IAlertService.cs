using LogAlertingSystem.Domain.Entities;

namespace LogAlertingSystem.Application.Interfaces;

public interface IAlertService
{
    Task<List<Alert>> EvaluateAndGenerateAlertsAsync(List<Log> logs);
}
