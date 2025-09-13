using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using WhoAndWhat.Application.DTOs.Calendar;
using WhoAndWhat.Infrastructure.Configuration;
using WhoAndWhat.Infrastructure.Services.Calendar;
using Xunit;

namespace WhoAndWhat.Infrastructure.Tests.Services;

/// <summary>
/// Tests for ICloudCalDAVProviderService, focusing on the substring bounds logic fix
/// </summary>
public class ICloudCalDAVProviderServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<ICloudCalDAVProviderService>> _mockLogger;
    private readonly IOptions<CalendarProviderSettings> _settings;
    private readonly HttpClient _httpClient;
    private readonly ICloudCalDAVProviderService _service;
    private readonly Guid _testUserId = Guid.NewGuid();
    private bool _disposed;

    public ICloudCalDAVProviderServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<ICloudCalDAVProviderService>>();
        
        _settings = Options.Create(new CalendarProviderSettings
        {
            Google = new ProviderConfig { ClientId = "test", ClientSecret = "test" },
            Outlook = new ProviderConfig { ClientId = "test", ClientSecret = "test" },
            ICloud = new ProviderConfig 
            { 
                ClientId = "test", 
                ClientSecret = "test",
                BaseUrl = "https://caldav.icloud.com"
            }
        });

        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _service = new ICloudCalDAVProviderService(_httpClient, _settings, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var service = new ICloudCalDAVProviderService(_httpClient, _settings, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.ProviderType.Should().Be(CalendarProvider.ICloud);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ICloudCalDAVProviderService(null!, _settings, _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ICloudCalDAVProviderService(_httpClient, null!, _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Theory]
    [InlineData("SUMMARY:Test Event")]
    [InlineData("DTSTART:20240101T120000Z")]
    [InlineData("DTEND:20240101T130000Z")]
    [InlineData("DESCRIPTION:This is a test description")]
    [InlineData("LOCATION:Conference Room A")]
    public async Task ParseCalendarEvents_WithValidPropertyValuePairs_ShouldParseCorrectly(string calendarLine)
    {
        // Arrange
        var icalData = CreateValidICalData(calendarLine);
        SetupHttpResponse(icalData);

        // Act
        var result = await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var colonIndex = calendarLine.IndexOf(':');
        var expectedProperty = calendarLine.Substring(0, colonIndex);
        var expectedValue = calendarLine.Substring(colonIndex + 1);
        
        result.First().Properties.Should().ContainKey(expectedProperty);
        result.First().Properties[expectedProperty].Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("SUMMARY:")]  // Colon at end - empty value
    [InlineData("DTSTART:")]  // Colon at end - empty value
    [InlineData("DESCRIPTION:")]  // Colon at end - empty value
    public async Task ParseCalendarEvents_WithColonAtEnd_ShouldHandleEmptyValues(string calendarLine)
    {
        // Arrange
        var icalData = CreateValidICalData(calendarLine);
        SetupHttpResponse(icalData);

        // Act
        var result = await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var property = calendarLine.TrimEnd(':');
        result.First().Properties.Should().ContainKey(property);
        result.First().Properties[property].Should().BeEmpty();
    }

    [Theory]
    [InlineData("DTSTART;VALUE=DATE:20240101")]  // Property with parameter
    [InlineData("DTEND;TZID=America/New_York:20240101T130000")]  // Property with timezone parameter
    [InlineData("RRULE;FREQ=WEEKLY:BYDAY=MO,WE,FR")]  // Complex property with parameters
    public async Task ParseCalendarEvents_WithPropertyParameters_ShouldParsePropertyNameCorrectly(string calendarLine)
    {
        // Arrange
        var icalData = CreateValidICalData(calendarLine);
        SetupHttpResponse(icalData);

        // Act
        var result = await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var colonIndex = calendarLine.IndexOf(':');
        var propertyPart = calendarLine.Substring(0, colonIndex);
        var expectedPropertyName = propertyPart.Split(';')[0]; // First part before semicolon
        
        result.First().Properties.Should().ContainKey(expectedPropertyName);
    }

    [Theory]
    [InlineData("SUMMARY")]  // Property without colon (invalid)
    [InlineData("")]  // Empty line
    [InlineData("   ")]  // Whitespace only
    public async Task ParseCalendarEvents_WithInvalidLines_ShouldSkipGracefully(string calendarLine)
    {
        // Arrange
        var icalData = CreateInvalidICalData(calendarLine);
        SetupHttpResponse(icalData);

        // Act
        var act = async () => await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert - Should not throw exception, should handle gracefully
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ParseCalendarEvents_WithMultipleColons_ShouldUseFirstColonAsSeparator()
    {
        // Arrange
        var calendarLine = "DESCRIPTION:Meeting at 3:30 PM in Room 101";
        var icalData = CreateValidICalData(calendarLine);
        SetupHttpResponse(icalData);

        // Act
        var result = await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.First().Properties.Should().ContainKey("DESCRIPTION");
        result.First().Properties["DESCRIPTION"].Should().Be("Meeting at 3:30 PM in Room 101");
    }

    [Fact]
    public async Task ParseCalendarEvents_WithSpecialCharactersInValue_ShouldPreserveContent()
    {
        // Arrange
        var calendarLine = "SUMMARY:Test Event with Special: Characters; & More!";
        var icalData = CreateValidICalData(calendarLine);
        SetupHttpResponse(icalData);

        // Act
        var result = await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.First().Properties.Should().ContainKey("SUMMARY");
        result.First().Properties["SUMMARY"].Should().Be("Test Event with Special: Characters; & More!");
    }

    [Fact]
    public async Task ParseCalendarEvents_ConcurrentParsing_ShouldNotCauseRaceConditions()
    {
        // Arrange
        var icalData = CreateComplexICalData();
        SetupHttpResponse(icalData);

        // Act - Run multiple parsing operations concurrently
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            return await _service.GetCalendarEventsAsync(_testUserId, DateTime.Today, DateTime.Today.AddDays(1));
        });

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be identical and valid
        results.Should().AllSatisfy(result => 
        {
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        });

        // All results should have the same count
        var firstCount = results.First().Count();
        results.Should().AllSatisfy(result => result.Count().Should().Be(firstCount));
    }

    private string CreateValidICalData(string customLine)
    {
        return $@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:test-event-{Guid.NewGuid()}
DTSTART:20240101T120000Z
DTEND:20240101T130000Z
{customLine}
END:VEVENT
END:VCALENDAR";
    }

    private string CreateInvalidICalData(string invalidLine)
    {
        return $@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:test-event-{Guid.NewGuid()}
DTSTART:20240101T120000Z
DTEND:20240101T130000Z
{invalidLine}
SUMMARY:Valid Event
END:VEVENT
END:VCALENDAR";
    }

    private string CreateComplexICalData()
    {
        return @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:test-event-1
DTSTART:20240101T120000Z
DTEND:20240101T130000Z
SUMMARY:Event with: multiple colons
DESCRIPTION:Meeting at 3:30 PM in Room 101
LOCATION:Conference Room A
END:VEVENT
BEGIN:VEVENT
UID:test-event-2
DTSTART;VALUE=DATE:20240102
DTEND;VALUE=DATE:20240103
SUMMARY:All Day Event
DESCRIPTION:
END:VEVENT
END:VCALENDAR";
    }

    private void SetupHttpResponse(string content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "text/calendar")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}