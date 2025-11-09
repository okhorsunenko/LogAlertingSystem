using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;

namespace LogAlertingSystem.Application.Interfaces;

public interface IAlertRepository
{
    Task<Alert?> GetByIdAsync(int id);
    Task<List<Alert>> GetAllAsync(int skip = 0, int take = 100);
    Task<List<Alert>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task AddAsync(Alert alert);
    Task AddRangeAsync(List<Alert> alerts);
    Task UpdateAsync(Alert alert);
    Task<int> SaveChangesAsync();
}
