using LogAlertingSystem.Domain.Entities;

namespace LogAlertingSystem.Application.Interfaces;

public interface ILogIngestionService
{
    /// <summary>
    /// Initializes bookmarks based on the last log in the database
    /// Should be called once on application startup
    /// </summary>
    Task InitializeBookmarksAsync();

    /// <summary>
    /// Reads new logs since last check
    /// </summary>
    /// <returns>List of new normalized logs</returns>
    Task<List<Log>> GetNewLogsAsync();

    /// <summary>
    /// Reads logs from a specific time range
    /// </summary>
    /// <param name="startTime">Start time for log retrieval</param>
    /// <param name="endTime">End time for log retrieval</param>
    /// <returns>List of normalized logs</returns>
    //Task<List<Log>> GetLogsAsync(DateTime? startTime = null, DateTime? endTime = null);
}
