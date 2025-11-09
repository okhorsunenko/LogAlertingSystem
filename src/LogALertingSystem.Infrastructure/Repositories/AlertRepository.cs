using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Domain.Enums;
using LogAlertingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogAlertingSystem.Infrastructure.Repositories;

public class AlertRepository : IAlertRepository
{
    private readonly ApplicationDbContext _context;

    public AlertRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Alert?> GetByIdAsync(int id)
    {
        return await _context.Alerts
            .Include(a => a.AlertRule)
            .Include(a => a.Log)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Alert>> GetAllAsync(int skip = 0, int take = 100)
    {
        return await _context.Alerts
            .Include(a => a.AlertRule)
            .Include(a => a.Log)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Alert>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Alerts
            .Include(a => a.AlertRule)
            .Include(a => a.Log)
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Alert alert)
    {
        await _context.Alerts.AddAsync(alert);
    }

    public async Task AddRangeAsync(List<Alert> alerts)
    {
        await _context.Alerts.AddRangeAsync(alerts);
    }

    public async Task UpdateAsync(Alert alert)
    {
        _context.Alerts.Update(alert);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
