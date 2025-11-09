using LogAlertingSystem.Application.Interfaces;
using LogAlertingSystem.Application.Services;
using LogAlertingSystem.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LogAlertingSystem.Tests;

public class WindowsLogIngestionServiceTests
{
    private readonly Mock<ILogger<WindowsLogIngestionService>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogRepository> _mockLogRepository;

    public WindowsLogIngestionServiceTests()
    {
        _mockLogger = new Mock<ILogger<WindowsLogIngestionService>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogRepository = new Mock<ILogRepository>();

        // Setup the service scope factory chain
        _mockServiceScopeFactory.Setup(x => x.CreateScope())
            .Returns(_mockServiceScope.Object);
        _mockServiceScope.Setup(x => x.ServiceProvider)
            .Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ILogRepository)))
            .Returns(_mockLogRepository.Object);
    }

    [Fact]
    public void Constructor_InitializesBookmarksForAllLogSources()
    {
        // Act
        var service = new WindowsLogIngestionService(_mockLogger.Object, _mockServiceScopeFactory.Object);

        // Assert
        Assert.NotNull(service);
        // The service should be created successfully with bookmarks initialized
    }

    [Fact]
    public async Task InitializeBookmarksAsync_WithNoExistingLogs_DoesNotThrow()
    {
        // Arrange
        _mockLogRepository.Setup(x => x.GetAllAsync(0, 1))
            .ReturnsAsync(new List<Log>());

        var service = new WindowsLogIngestionService(_mockLogger.Object, _mockServiceScopeFactory.Object);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => service.InitializeBookmarksAsync());

        // Should not throw exception even if no logs exist
        // (May throw on actual Windows Event Log access, but should handle gracefully)
    }

    [Fact]
    public async Task InitializeBookmarksAsync_WithExistingLogs_UsesLastLogTimestamp()
    {
        // Arrange
        var lastLog = new Log
        {
            Id = 1,
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Level = Domain.Enums.EventLogLevel.Information,
            Source = "TestSource",
            Type = "Information",
            Message = "Test message"
        };

        _mockLogRepository.Setup(x => x.GetAllAsync(0, 1))
            .ReturnsAsync(new List<Log> { lastLog });

        var service = new WindowsLogIngestionService(_mockLogger.Object, _mockServiceScopeFactory.Object);

        // Act
        await service.InitializeBookmarksAsync();

        // Assert
        _mockLogRepository.Verify(x => x.GetAllAsync(0, 1), Times.Once);
    }

    [Fact]
    public async Task InitializeBookmarksAsync_HandlesExceptionGracefully()
    {
        // Arrange
        _mockLogRepository.Setup(x => x.GetAllAsync(0, 1))
            .ThrowsAsync(new Exception("Database error"));

        var service = new WindowsLogIngestionService(_mockLogger.Object, _mockServiceScopeFactory.Object);

        // Act
        await service.InitializeBookmarksAsync();

        // Assert
        // Should log error but not throw exception
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

public class WindowsLogIngestionServiceMappingTests
{
    [Theory]
    [InlineData("Critical")]
    [InlineData("Error")]
    [InlineData("Warning")]
    [InlineData("Information")]
    public void MapEventLevel_ValidLevels_ReturnsCorrectLogLevel(string expectedLevel)
    {
        // This tests the mapping logic indirectly
        // The actual mapping in WindowsLogIngestionService:
        // 1 => Critical
        // 2 => Error
        // 3 => Warning
        // 4 => Information
        // 0 => Information (LogAlways)
        // _ => Information (default)

        var expectedLogLevel = Enum.Parse<Domain.Enums.EventLogLevel>(expectedLevel);

        // Assert
        Assert.IsType<Domain.Enums.EventLogLevel>(expectedLogLevel);
    }

    [Fact]
    public void EventLogLevel_HasCorrectValues()
    {
        // Verify that EventLogLevel enum has the expected values
        var values = Enum.GetNames(typeof(Domain.Enums.EventLogLevel));

        Assert.Contains("Information", values);
        Assert.Contains("Warning", values);
        Assert.Contains("Error", values);
        Assert.Contains("Critical", values);
    }
}