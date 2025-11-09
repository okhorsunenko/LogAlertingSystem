using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;

namespace LogAlertingSystem.Application.Interfaces;

public interface ILogRepository
{
    Task<Log?> GetByIdAsync(int id);
    Task<List<Log>> GetAllAsync(int skip = 0, int take = 100);
    Task<int> GetCountAsync();
    Task<List<Log>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<Log>> GetByLevelAsync(EventLogLevel level, int skip = 0, int take = 100);
    Task<List<Log>> GetBySourceAsync(string source, int skip = 0, int take = 100);
    Task AddAsync(Log log);
    Task AddRangeAsync(List<Log> logs);
    Task<int> SaveChangesAsync();
}
