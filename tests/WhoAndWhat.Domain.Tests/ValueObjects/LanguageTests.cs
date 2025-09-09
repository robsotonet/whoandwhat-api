using FluentAssertions;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the Language enum value object
/// </summary>
public class LanguageTests
{
    [Fact]
    public void Language_Should_Have_All_Expected_Values()
    {
        // Arrange
        var expectedValues = new[]
        {
            Language.en,
            Language.es
        };

        // Act
        var actualValues = Enum.GetValues<Language>();

        // Assert
        actualValues.Should().HaveCount(2);
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData(Language.en, 0)]
    [InlineData(Language.es, 1)]
    public void Language_Should_Have_Expected_Numeric_Values(Language language, int expectedValue)
    {
        // Act & Assert
        ((int)language).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(Language.en, "en")]
    [InlineData(Language.es, "es")]
    public void Language_Should_Convert_To_String(Language language, string expectedString)
    {
        // Act & Assert
        language.ToString().Should().Be(expectedString);
    }

    [Theory]
    [InlineData("en", Language.en)]
    [InlineData("es", Language.es)]
    [InlineData("EN", Language.en)] // Case insensitive
    [InlineData("ES", Language.es)] // Case insensitive
    public void Language_Should_Parse_From_String(string input, Language expected)
    {
        // Act & Assert
        Enum.Parse<Language>(input, true).Should().Be(expected);
    }

    [Theory]
    [InlineData("fr")] // French - not supported
    [InlineData("de")] // German - not supported  
    [InlineData("")]
    [InlineData(null)]
    [InlineData("english")] // Full name instead of code
    [InlineData("spanish")] // Full name instead of code
    public void Language_Should_Throw_On_Invalid_Parse(string input)
    {
        // Act & Assert
        Action act = () => Enum.Parse<Language>(input, true);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("en", true, Language.en)]
    [InlineData("es", true, Language.es)]
    [InlineData("EN", true, Language.en)]
    [InlineData("ES", true, Language.es)]
    [InlineData("fr", false, Language.en)] // Default when parsing fails
    [InlineData("", false, Language.en)]
    [InlineData("invalid", false, Language.en)]
    public void Language_Should_TryParse_Correctly(string input, bool expectedSuccess, Language expectedValue)
    {
        // Act
        var success = Enum.TryParse<Language>(input, true, out var result);

        // Assert
        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            result.Should().Be(expectedValue);
        }
    }

    [Fact]
    public void Language_Should_Be_Defined_For_All_Values()
    {
        // Arrange
        var values = Enum.GetValues<Language>();

        // Act & Assert
        foreach (var value in values)
        {
            Enum.IsDefined(typeof(Language), value).Should().BeTrue($"Value {value} should be defined");
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]  // en
    [InlineData(1, true)]  // es
    [InlineData(2, false)] // Out of range
    [InlineData(100, false)] // Way out of range
    public void Language_Should_Validate_Numeric_Values(int value, bool shouldBeDefined)
    {
        // Act & Assert
        Enum.IsDefined(typeof(Language), value).Should().Be(shouldBeDefined);
    }

    [Fact]
    public void Language_Should_Support_Comparison()
    {
        // Arrange
        var english = Language.en;
        var spanish = Language.es;

        // Act & Assert
        (english < spanish).Should().BeTrue();
        (english == Language.en).Should().BeTrue();
        (english != spanish).Should().BeTrue();
        (spanish > english).Should().BeTrue();
    }

    [Fact]
    public void Language_Should_Be_Bilingual_System()
    {
        // Arrange
        var allLanguages = Enum.GetValues<Language>();

        // Act & Assert
        allLanguages.Should().HaveCount(2, "The system should support exactly 2 languages as specified in requirements");
        allLanguages.Should().Contain(Language.en, "English should be supported");
        allLanguages.Should().Contain(Language.es, "Spanish should be supported");
    }

    [Theory]
    [InlineData(Language.en, "English")]
    [InlineData(Language.es, "Spanish")]
    public void Language_Should_Map_To_Display_Names(Language language, string expectedDisplayName)
    {
        // This test demonstrates how language codes could be mapped to display names
        // This would typically be handled by localization resources
        
        // Act
        string displayName = language switch
        {
            Language.en => "English",
            Language.es => "Spanish",
            _ => language.ToString()
        };

        // Assert
        displayName.Should().Be(expectedDisplayName);
    }

    [Theory]
    [InlineData(Language.en, "en-US")]
    [InlineData(Language.es, "es-ES")]
    public void Language_Should_Map_To_Culture_Codes(Language language, string expectedCultureCode)
    {
        // This test demonstrates how language enum could map to culture codes
        // This would be useful for localization and formatting
        
        // Act
        string cultureCode = language switch
        {
            Language.en => "en-US",
            Language.es => "es-ES", 
            _ => "en-US" // Default fallback
        };

        // Assert
        cultureCode.Should().Be(expectedCultureCode);
    }
}