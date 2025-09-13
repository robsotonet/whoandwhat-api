using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Moq;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Domain.Tests.Builders;
using WhoAndWhat.Domain.Tests.Helpers;

namespace WhoAndWhat.Domain.Tests.Fixtures;

/// <summary>
/// Test fixture for motivational content system tests
/// Provides mocked repositories and common test data
/// </summary>
public class MotivationalContentTestFixture : IDisposable
{
    // Mock repositories
    public Mock<IRepository<MotivationalContent>> MockContentRepository { get; }
    public Mock<IRepository<UserContentPreferences>> MockPreferencesRepository { get; }
    public Mock<IRepository<ContentDeliveryLog>> MockDeliveryLogRepository { get; }
    public Mock<ILogger> MockLogger { get; }

    // Test data collections
    public List<MotivationalContent> TestContents { get; }
    public List<UserContentPreferences> TestPreferences { get; }
    public List<ContentDeliveryLog> TestDeliveryLogs { get; }
    public List<Guid> TestUserIds { get; }

    // In-memory storage for mock repositories
    private readonly List<MotivationalContent> _contentStorage;
    private readonly List<UserContentPreferences> _preferencesStorage;
    private readonly List<ContentDeliveryLog> _deliveryLogStorage;

    public MotivationalContentTestFixture()
    {
        // Initialize mocks
        MockContentRepository = new Mock<IRepository<MotivationalContent>>();
        MockPreferencesRepository = new Mock<IRepository<UserContentPreferences>>();
        MockDeliveryLogRepository = new Mock<IRepository<ContentDeliveryLog>>();
        MockLogger = new Mock<ILogger>();

        // Initialize test data
        TestUserIds = MotivationalContentTestHelper.CreateTestUserIds(5);
        TestContents = MotivationalContentTestHelper.CreateDiverseContentSet();
        TestPreferences = MotivationalContentTestHelper.CreateUserArchetypes();
        TestDeliveryLogs = CreateTestDeliveryLogs();

        // Initialize storage
        _contentStorage = new List<MotivationalContent>(TestContents);
        _preferencesStorage = new List<UserContentPreferences>(TestPreferences);
        _deliveryLogStorage = new List<ContentDeliveryLog>(TestDeliveryLogs);

        SetupMockRepositories();
    }

    /// <summary>
    /// Creates comprehensive delivery logs for testing
    /// </summary>
    private List<ContentDeliveryLog> CreateTestDeliveryLogs()
    {
        var logs = new List<ContentDeliveryLog>();

        // Create realistic logs for each user and content combination
        foreach (var userId in TestUserIds.Take(3)) // Limit for performance
        {
            var userLogs = MotivationalContentTestHelper.CreateRealisticDeliveryLogs(
                userId,
                TestContents.Take(5).ToList(), // Limit contents for performance
                logsPerContent: 8,
                overallEngagementRate: 0.65);
            logs.AddRange(userLogs);
        }

        // Add A/B testing data
        var abTestLogs = MotivationalContentTestHelper.CreateABTestingData(
            TestUserIds[0],
            TestContents[0].Id,
            groupAEngagementRate: 0.6,
            groupBEngagementRate: 0.8,
            logsPerGroup: 50);
        logs.AddRange(abTestLogs);

        // Add time series data
        var timeSeriesLogs = MotivationalContentTestHelper.CreateTimeSeriesData(
            TestUserIds[1],
            TestContents[1].Id,
            days: 30,
            deliveriesPerDay: 2);
        logs.AddRange(timeSeriesLogs);

        return logs;
    }

    /// <summary>
    /// Sets up mock repository behaviors
    /// </summary>
    private void SetupMockRepositories()
    {
        SetupContentRepositoryMock();
        SetupPreferencesRepositoryMock();
        SetupDeliveryLogRepositoryMock();
    }

    /// <summary>
    /// Sets up MotivationalContent repository mock
    /// </summary>
    private void SetupContentRepositoryMock()
    {
        // GetByIdAsync
        MockContentRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _contentStorage.FirstOrDefault(c => c.Id == id));

        // GetAllAsync
        MockContentRepository
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _contentStorage.AsEnumerable());

        // FindAsync
        MockContentRepository
            .Setup(repo => repo.FindAsync(It.IsAny<Expression<Func<MotivationalContent, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<MotivationalContent, bool>> predicate, CancellationToken _) =>
                _contentStorage.Where(predicate.Compile()));

        // AddAsync
        MockContentRepository
            .Setup(repo => repo.AddAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()))
            .Callback<MotivationalContent, CancellationToken>((content, _) => _contentStorage.Add(content))
            .Returns(Task.CompletedTask);

        // UpdateAsync
        MockContentRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()))
            .Callback<MotivationalContent, CancellationToken>((content, _) =>
            {
                var existing = _contentStorage.FirstOrDefault(c => c.Id == content.Id);
                if (existing != null)
                {
                    var index = _contentStorage.IndexOf(existing);
                    _contentStorage[index] = content;
                }
            })
            .Returns(Task.CompletedTask);

        // DeleteAsync
        MockContentRepository
            .Setup(repo => repo.DeleteAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()))
            .Callback<MotivationalContent, CancellationToken>((content, _) => _contentStorage.Remove(content))
            .Returns(Task.CompletedTask);

        // SaveChangesAsync
        MockContentRepository
            .Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up UserContentPreferences repository mock
    /// </summary>
    private void SetupPreferencesRepositoryMock()
    {
        // GetByIdAsync
        MockPreferencesRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _preferencesStorage.FirstOrDefault(p => p.Id == id));

        // GetAllAsync
        MockPreferencesRepository
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _preferencesStorage.AsEnumerable());

        // FindAsync
        MockPreferencesRepository
            .Setup(repo => repo.FindAsync(It.IsAny<Expression<Func<UserContentPreferences, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<UserContentPreferences, bool>> predicate, CancellationToken _) =>
                _preferencesStorage.Where(predicate.Compile()));

        // AddAsync
        MockPreferencesRepository
            .Setup(repo => repo.AddAsync(It.IsAny<UserContentPreferences>(), It.IsAny<CancellationToken>()))
            .Callback<UserContentPreferences, CancellationToken>((preferences, _) => _preferencesStorage.Add(preferences))
            .Returns(Task.CompletedTask);

        // UpdateAsync
        MockPreferencesRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<UserContentPreferences>(), It.IsAny<CancellationToken>()))
            .Callback<UserContentPreferences, CancellationToken>((preferences, _) =>
            {
                var existing = _preferencesStorage.FirstOrDefault(p => p.Id == preferences.Id);
                if (existing != null)
                {
                    var index = _preferencesStorage.IndexOf(existing);
                    _preferencesStorage[index] = preferences;
                }
            })
            .Returns(Task.CompletedTask);

        // SaveChangesAsync
        MockPreferencesRepository
            .Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Sets up ContentDeliveryLog repository mock
    /// </summary>
    private void SetupDeliveryLogRepositoryMock()
    {
        // GetByIdAsync
        MockDeliveryLogRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _deliveryLogStorage.FirstOrDefault(l => l.Id == id));

        // GetAllAsync
        MockDeliveryLogRepository
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _deliveryLogStorage.AsEnumerable());

        // FindAsync
        MockDeliveryLogRepository
            .Setup(repo => repo.FindAsync(It.IsAny<Expression<Func<ContentDeliveryLog, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<ContentDeliveryLog, bool>> predicate, CancellationToken _) =>
                _deliveryLogStorage.Where(predicate.Compile()));

        // AddAsync
        MockDeliveryLogRepository
            .Setup(repo => repo.AddAsync(It.IsAny<ContentDeliveryLog>(), It.IsAny<CancellationToken>()))
            .Callback<ContentDeliveryLog, CancellationToken>((log, _) => _deliveryLogStorage.Add(log))
            .Returns(Task.CompletedTask);

        // SaveChangesAsync
        MockDeliveryLogRepository
            .Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    /// <summary>
    /// Resets all mock repositories to their initial state
    /// </summary>
    public void ResetRepositories()
    {
        _contentStorage.Clear();
        _contentStorage.AddRange(TestContents);

        _preferencesStorage.Clear();
        _preferencesStorage.AddRange(TestPreferences);

        _deliveryLogStorage.Clear();
        _deliveryLogStorage.AddRange(TestDeliveryLogs);

        MockContentRepository.Reset();
        MockPreferencesRepository.Reset();
        MockDeliveryLogRepository.Reset();
        MockLogger.Reset();

        SetupMockRepositories();
    }

    /// <summary>
    /// Gets current storage state for verification
    /// </summary>
    public (List<MotivationalContent> contents, List<UserContentPreferences> preferences, List<ContentDeliveryLog> logs)
        GetCurrentStorageState()
    {
        return (_contentStorage.ToList(), _preferencesStorage.ToList(), _deliveryLogStorage.ToList());
    }

    /// <summary>
    /// Adds test content to storage
    /// </summary>
    public void AddTestContent(MotivationalContent content)
    {
        _contentStorage.Add(content);
    }

    /// <summary>
    /// Adds test preferences to storage
    /// </summary>
    public void AddTestPreferences(UserContentPreferences preferences)
    {
        _preferencesStorage.Add(preferences);
    }

    /// <summary>
    /// Adds test delivery log to storage
    /// </summary>
    public void AddTestDeliveryLog(ContentDeliveryLog log)
    {
        _deliveryLogStorage.Add(log);
    }

    /// <summary>
    /// Creates a new content builder with the fixture's test data
    /// </summary>
    public MotivationalContentBuilder CreateContentBuilder()
    {
        return MotivationalContentBuilder.New();
    }

    /// <summary>
    /// Creates a new preferences builder with the fixture's test data
    /// </summary>
    public UserContentPreferencesBuilder CreatePreferencesBuilder()
    {
        return UserContentPreferencesBuilder.New().ForUser(Guid.NewGuid());
    }

    /// <summary>
    /// Creates a new delivery log builder with the fixture's test data
    /// </summary>
    public ContentDeliveryLogBuilder CreateDeliveryLogBuilder()
    {
        return ContentDeliveryLogBuilder.New()
            .ForUser(TestUserIds[0])
            .ForContent(TestContents[0].Id);
    }

    /// <summary>
    /// Sets up a scenario where content repository returns specific data
    /// </summary>
    public void SetupContentRepositoryScenario(List<MotivationalContent> contents)
    {
        _contentStorage.Clear();
        _contentStorage.AddRange(contents);
    }

    /// <summary>
    /// Sets up a scenario where preferences repository returns specific data
    /// </summary>
    public void SetupPreferencesRepositoryScenario(List<UserContentPreferences> preferences)
    {
        _preferencesStorage.Clear();
        _preferencesStorage.AddRange(preferences);
    }

    /// <summary>
    /// Sets up a scenario where delivery log repository returns specific data
    /// </summary>
    public void SetupDeliveryLogRepositoryScenario(List<ContentDeliveryLog> logs)
    {
        _deliveryLogStorage.Clear();
        _deliveryLogStorage.AddRange(logs);
    }

    /// <summary>
    /// Verifies that a repository method was called with specific parameters
    /// </summary>
    public void VerifyContentRepositoryCalls(
        Times? getByIdTimes = null,
        Times? getAllTimes = null,
        Times? findTimes = null,
        Times? addTimes = null,
        Times? updateTimes = null,
        Times? deleteTimes = null,
        Times? saveChangesTimes = null)
    {
        if (getByIdTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), getByIdTimes.Value);
        }

        if (getAllTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), getAllTimes.Value);
        }

        if (findTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<MotivationalContent, bool>>>(), It.IsAny<CancellationToken>()), findTimes.Value);
        }

        if (addTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.AddAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()), addTimes.Value);
        }

        if (updateTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.UpdateAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()), updateTimes.Value);
        }

        if (deleteTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.DeleteAsync(It.IsAny<MotivationalContent>(), It.IsAny<CancellationToken>()), deleteTimes.Value);
        }

        if (saveChangesTimes.HasValue)
        {
            MockContentRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), saveChangesTimes.Value);
        }
    }

    public void Dispose()
    {
        // Clean up any resources if needed
        MockContentRepository?.Reset();
        MockPreferencesRepository?.Reset();
        MockDeliveryLogRepository?.Reset();
        MockLogger?.Reset();
    }
}
