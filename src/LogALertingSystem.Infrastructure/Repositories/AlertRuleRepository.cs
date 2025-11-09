using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Domain.Entities;
using LogAlertingSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogAlertingSystem.Infrastructure.Repositories;

public class AlertRuleRepository : IAlertRuleRepository
{
    private readonly ApplicationDbContext _context;

    public AlertRuleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AlertRule?> GetByIdAsync(int id)
    {
        return await _context.AlertRules.FindAsync(id);
    }

    public async Task<List<AlertRule>> GetAllAsync()
    {
        return await _context.AlertRules.ToListAsync();
    }

    public async Task<List<AlertRule>> GetActiveRulesAsync()
    {
        return await _context.AlertRules
            .Where(r => r.IsActive)
            .ToListAsync();
    }

    public async Task AddAsync(AlertRule alertRule)
    {
        await _context.AlertRules.AddAsync(alertRule);
    }

    public async Task UpdateAsync(AlertRule alertRule)
    {
        _context.AlertRules.Update(alertRule);
    }

    public async Task DeleteAsync(int id)
    {
        var alertRule = await _context.AlertRules.FindAsync(id);
        if (alertRule != null)
        {
            _context.AlertRules.Remove(alertRule);
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
