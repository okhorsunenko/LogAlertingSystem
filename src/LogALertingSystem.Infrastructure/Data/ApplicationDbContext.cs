using LogAlertingSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogAlertingSystem.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Log> Logs { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<AlertRule> AlertRules { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Log entity
        builder.Entity<Log>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Source).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Level);
        });

        // Configure AlertRule entity
        builder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageContainsCondition).HasMaxLength(500);
            entity.Property(e => e.MessageEqualCondition).HasMaxLength(500);
            entity.Property(e => e.SourceContainsCondition).HasMaxLength(500);
            entity.Property(e => e.SourceEqualCondition).HasMaxLength(500);
            entity.Property(e => e.TypeContainsCondition).HasMaxLength(500);
            entity.Property(e => e.TypeEqualCondition).HasMaxLength(500);
            entity.Property(e => e.LogLevel).IsRequired(false);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.LogLevel);
        });

        // Configure Alert entity
        builder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Message).IsRequired();

            // Relationship with AlertRule
            entity.HasOne(e => e.AlertRule)
                .WithMany()
                .HasForeignKey(e => e.AlertRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Log
            entity.HasOne(e => e.Log)
                .WithMany()
                .HasForeignKey(e => e.LogId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
