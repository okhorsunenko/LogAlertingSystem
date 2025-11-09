using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using LogAlertingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogAlertingSystem.Infrastructure.Repositories;

public class LogRepository : ILogRepository
{
    private readonly ApplicationDbContext _context;

    public LogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Log?> GetByIdAsync(int id)
    {
        return await _context.Logs.FindAsync(id);
    }

    public async Task<List<Log>> GetAllAsync(int skip = 0, int take = 100)
    {
        return await _context.Logs
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.Logs.CountAsync();
    }

    public async Task<List<Log>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Logs
            .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<Log>> GetByLevelAsync(EventLogLevel level, int skip = 0, int take = 100)
    {
        return await _context.Logs
            .Where(l => l.Level == level)
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Log>> GetBySourceAsync(string source, int skip = 0, int take = 100)
    {
        return await _context.Logs
            .Where(l => l.Source == source)
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task AddAsync(Log log)
    {
        await _context.Logs.AddAsync(log);
    }

    public async Task AddRangeAsync(List<Log> logs)
    {
        await _context.Logs.AddRangeAsync(logs);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
