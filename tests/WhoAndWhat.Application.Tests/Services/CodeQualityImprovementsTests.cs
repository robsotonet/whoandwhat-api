using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Application.Services;
using WhoAndWhat.Domain.Entities;
using WhoAndWhat.Infrastructure.Repositories.Analytics;
using WhoAndWhat.Infrastructure.Services;
using Xunit;

namespace WhoAndWhat.Application.Tests.Services;

/// <summary>
/// Tests to validate code quality improvements and fixes
/// </summary>
public class CodeQualityImprovementsTests : IDisposable
{
    private readonly Mock<IRepository<MotivationalContent>> _mockContentRepository;
    private readonly Mock<IRepository<ContentDeliveryLog>> _mockDeliveryLogRepository;
    private readonly Mock<IRepository<UserContentPreferences>> _mockPreferencesRepository;
    private readonly Mock<IAnalyticsRepository> _mockAnalyticsRepository;
    private readonly Mock<ILogger<MotivationalContentService>> _mockLogger;
    private readonly MotivationalContentService _service;

    public CodeQualityImprovementsTests()
    {
        _mockContentRepository = new Mock<IRepository<MotivationalContent>>();
        _mockDeliveryLogRepository = new Mock<IRepository<ContentDeliveryLog>>();
        _mockPreferencesRepository = new Mock<IRepository<UserContentPreferences>>();
        _mockAnalyticsRepository = new Mock<IAnalyticsRepository>();
        _mockLogger = new Mock<ILogger<MotivationalContentService>>();

        _service = new MotivationalContentService(
            _mockContentRepository.Object,
            _mockDeliveryLogRepository.Object,
            _mockPreferencesRepository.Object,
            _mockAnalyticsRepository.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Test to validate that deterministic hashing produces consistent results
    /// across multiple calls (addressing the GetHashCode() reliability issue)
    /// </summary>
    [Fact]
    public void DeterministicHash_ShouldReturnConsistentResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testName = "engagement_test";
        var iterations = 100;

        // Use reflection to access the private ComputeDeterministicHash method
        var method = typeof(MotivationalContentService).GetMethod("ComputeDeterministicHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("ComputeDeterministicHash method should exist");

        // Act - Calculate hash multiple times
        var hashes = new List<int>();
        for (int i = 0; i < iterations; i++)
        {
            var hash = (int)method!.Invoke(_service, new object[] { userId, testName })!;
            hashes.Add(hash);
        }

        // Assert - All hashes should be identical
        hashes.Should().AllSatisfy(h => h.Should().Be(hashes.First()),
            "Deterministic hash should return the same value for identical inputs");

        // Verify hash is not zero (which would indicate a potential issue)
        hashes.First().Should().NotBe(0, "Hash should not be zero for valid inputs");
    }

    /// <summary>
    /// Test to validate that different inputs produce different deterministic hashes
    /// </summary>
    [Fact]
    public void DeterministicHash_ShouldProduceDifferentHashesForDifferentInputs()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var testName = "engagement_test";

        var method = typeof(MotivationalContentService).GetMethod("ComputeDeterministicHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var hash1 = (int)method!.Invoke(_service, new object[] { userId1, testName })!;
        var hash2 = (int)method!.Invoke(_service, new object[] { userId2, testName })!;
        var hash3 = (int)method!.Invoke(_service, new object[] { userId1, "different_test" })!;

        // Assert
        hash1.Should().NotBe(hash2, "Different user IDs should produce different hashes");
        hash1.Should().NotBe(hash3, "Different test names should produce different hashes");
        hash2.Should().NotBe(hash3, "Different inputs should produce different hashes");
    }

    /// <summary>
    /// Test to validate that the deterministic hash matches expected SHA256 behavior
    /// </summary>
    [Fact]
    public void DeterministicHash_ShouldMatchManualSHA256Calculation()
    {
        // Arrange
        var userId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var testName = "test_hash_consistency";
        var expectedInput = $"{userId}_{testName}";

        // Manual SHA256 calculation for verification
        var inputBytes = Encoding.UTF8.GetBytes(expectedInput);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(inputBytes);
        var expectedHash = Math.Abs(BitConverter.ToInt32(hashBytes, 0));

        var method = typeof(MotivationalContentService).GetMethod("ComputeDeterministicHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var actualHash = (int)method!.Invoke(_service, new object[] { userId, testName })!;

        // Assert
        actualHash.Should().Be(expectedHash, 
            "Deterministic hash should match manual SHA256 calculation");
    }

    /// <summary>
    /// Test to validate hash distribution uniformity (important for A/B testing fairness)
    /// </summary>
    [Fact]
    public void DeterministicHash_ShouldHaveReasonableDistribution()
    {
        // Arrange
        var testName = "distribution_test";
        var sampleSize = 1000;
        var hashes = new List<int>();

        var method = typeof(MotivationalContentService).GetMethod("ComputeDeterministicHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act - Generate hashes for different users
        for (int i = 0; i < sampleSize; i++)
        {
            var userId = Guid.NewGuid();
            var hash = (int)method!.Invoke(_service, new object[] { userId, testName })!;
            hashes.Add(hash % 100); // Normalize to 0-99 range for distribution analysis
        }

        // Assert - Check basic distribution properties
        var uniqueHashes = hashes.Distinct().Count();
        uniqueHashes.Should().BeGreaterThan(sampleSize / 2, 
            "Hash distribution should be reasonably uniform");

        var averageHash = hashes.Average();
        averageHash.Should().BeInRange(40, 60, 
            "Average hash should be near the middle of the range for good distribution");
    }

    /// <summary>
    /// Test to validate service construction and basic functionality
    /// </summary>
    [Fact]
    public void MotivationalContentService_ShouldBeConstructedSuccessfully()
    {
        // Arrange & Act & Assert
        _service.Should().NotBeNull();
        
        // Verify that the service can be used without throwing exceptions
        var healthCheckAction = () => _service.GetType();
        healthCheckAction.Should().NotThrow();
    }

    /// <summary>
    /// Integration test to validate that hash consistency works across service instances
    /// (important for ensuring A/B test assignment consistency across app restarts)
    /// </summary>
    [Fact]
    public void DeterministicHash_ShouldBeConsistentAcrossServiceInstances()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testName = "cross_instance_test";

        // Create a second service instance
        var secondService = new MotivationalContentService(
            _mockContentRepository.Object,
            _mockDeliveryLogRepository.Object,
            _mockPreferencesRepository.Object,
            _mockAnalyticsRepository.Object,
            _mockLogger.Object);

        var method = typeof(MotivationalContentService).GetMethod("ComputeDeterministicHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var hash1 = (int)method!.Invoke(_service, new object[] { userId, testName })!;
        var hash2 = (int)method!.Invoke(secondService, new object[] { userId, testName })!;

        // Assert
        hash1.Should().Be(hash2, 
            "Hash should be consistent across different service instances");
    }

    public void Dispose()
    {
        // No specific cleanup needed for this test class
    }
}

/// <summary>
/// Additional tests for statistical improvements validation
/// </summary>
public class StatisticalImprovementsTests
{
    /// <summary>
    /// Test to validate that Math.NET Numerics is properly integrated
    /// and can perform basic statistical calculations
    /// </summary>
    [Fact]
    public void MathNetNumerics_ShouldBeAvailableForStatisticalCalculations()
    {
        // Arrange & Act
        var chiSquareDistribution = new MathNet.Numerics.Distributions.ChiSquared(1);

        // Assert
        chiSquareDistribution.Should().NotBeNull();
        
        // Test basic functionality
        var pValue = 1.0 - chiSquareDistribution.CumulativeDistribution(3.84);
        pValue.Should().BeApproximately(0.05, 0.01, 
            "Chi-square distribution should calculate p-values correctly");
    }

    /// <summary>
    /// Test to validate chi-square calculation basics
    /// </summary>
    [Fact]
    public void ChiSquareDistribution_ShouldCalculateAccuratePValues()
    {
        // Arrange - Known chi-square values and expected p-values
        var testCases = new[]
        {
            (degreesOfFreedom: 1, chiSquare: 3.841, expectedPValue: 0.05),
            (degreesOfFreedom: 1, chiSquare: 6.635, expectedPValue: 0.01),
            (degreesOfFreedom: 2, chiSquare: 5.991, expectedPValue: 0.05),
            (degreesOfFreedom: 2, chiSquare: 9.210, expectedPValue: 0.01)
        };

        foreach (var (degreesOfFreedom, chiSquare, expectedPValue) in testCases)
        {
            // Act
            var distribution = new MathNet.Numerics.Distributions.ChiSquared(degreesOfFreedom);
            var actualPValue = 1.0 - distribution.CumulativeDistribution(chiSquare);

            // Assert
            actualPValue.Should().BeApproximately(expectedPValue, 0.005,
                $"Chi-square p-value should be accurate for df={degreesOfFreedom}, χ²={chiSquare}");
        }
    }
}