using LogAlertingSystem.Domain.Entities;

namespace LogAlertingSystem.Application.Interfaces;

public interface IAlertRuleRepository
{
    Task<AlertRule?> GetByIdAsync(int id);
    Task<List<AlertRule>> GetAllAsync();
    Task<List<AlertRule>> GetActiveRulesAsync();
    Task AddAsync(AlertRule alertRule);
    Task UpdateAsync(AlertRule alertRule);
    Task DeleteAsync(int id);
    Task<int> SaveChangesAsync();
}
