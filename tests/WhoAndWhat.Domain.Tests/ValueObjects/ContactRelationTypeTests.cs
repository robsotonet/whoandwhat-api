using FluentAssertions;
using WhoAndWhat.Domain.ValueObjects;
using Xunit;

namespace WhoAndWhat.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the ContactRelationType enum value object
/// </summary>
public class ContactRelationTypeTests
{
    [Fact]
    public void ContactRelationType_Should_Have_All_Expected_Values()
    {
        // Arrange
        var expectedValues = new[]
        {
            ContactRelationType.Family,
            ContactRelationType.Friend,
            ContactRelationType.Colleague,
            ContactRelationType.Other
        };

        // Act
        var actualValues = Enum.GetValues<ContactRelationType>();

        // Assert
        actualValues.Should().HaveCount(4);
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData(ContactRelationType.Family, 0)]
    [InlineData(ContactRelationType.Friend, 1)]
    [InlineData(ContactRelationType.Colleague, 2)]
    [InlineData(ContactRelationType.Other, 3)]
    public void ContactRelationType_Should_Have_Expected_Numeric_Values(ContactRelationType relationType, int expectedValue)
    {
        // Act & Assert
        ((int)relationType).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(ContactRelationType.Family, "Family")]
    [InlineData(ContactRelationType.Friend, "Friend")]
    [InlineData(ContactRelationType.Colleague, "Colleague")]
    [InlineData(ContactRelationType.Other, "Other")]
    public void ContactRelationType_Should_Convert_To_String(ContactRelationType relationType, string expectedString)
    {
        // Act & Assert
        relationType.ToString().Should().Be(expectedString);
    }

    [Theory]
    [InlineData("Family", ContactRelationType.Family)]
    [InlineData("Friend", ContactRelationType.Friend)]
    [InlineData("Colleague", ContactRelationType.Colleague)]
    [InlineData("Other", ContactRelationType.Other)]
    [InlineData("family", ContactRelationType.Family)] // Case insensitive
    [InlineData("FRIEND", ContactRelationType.Friend)] // Case insensitive
    public void ContactRelationType_Should_Parse_From_String(string input, ContactRelationType expected)
    {
        // Act & Assert
        Enum.Parse<ContactRelationType>(input, true).Should().Be(expected);
    }

    [Theory]
    [InlineData("InvalidValue")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Business")] // Not a valid enum value
    public void ContactRelationType_Should_Throw_On_Invalid_Parse(string input)
    {
        // Act & Assert
        Action act = () => Enum.Parse<ContactRelationType>(input, true);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Family", true, ContactRelationType.Family)]
    [InlineData("Friend", true, ContactRelationType.Friend)]
    [InlineData("InvalidValue", false, ContactRelationType.Family)] // Default value when parsing fails
    [InlineData("", false, ContactRelationType.Family)]
    public void ContactRelationType_Should_TryParse_Correctly(string input, bool expectedSuccess, ContactRelationType expectedValue)
    {
        // Act
        var success = Enum.TryParse<ContactRelationType>(input, true, out var result);

        // Assert
        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            result.Should().Be(expectedValue);
        }
    }

    [Fact]
    public void ContactRelationType_Should_Be_Defined_For_All_Values()
    {
        // Arrange
        var values = Enum.GetValues<ContactRelationType>();

        // Act & Assert
        foreach (var value in values)
        {
            Enum.IsDefined(typeof(ContactRelationType), value).Should().BeTrue($"Value {value} should be defined");
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]  // Family
    [InlineData(1, true)]  // Friend
    [InlineData(2, true)]  // Colleague
    [InlineData(3, true)]  // Other
    [InlineData(4, false)] // Out of range
    [InlineData(100, false)] // Way out of range
    public void ContactRelationType_Should_Validate_Numeric_Values(int value, bool shouldBeDefined)
    {
        // Act & Assert
        Enum.IsDefined(typeof(ContactRelationType), value).Should().Be(shouldBeDefined);
    }

    [Fact]
    public void ContactRelationType_Should_Support_Comparison()
    {
        // Arrange
        var family = ContactRelationType.Family;
        var friend = ContactRelationType.Friend;
        var colleague = ContactRelationType.Colleague;
        var other = ContactRelationType.Other;

        // Act & Assert
        (family < friend).Should().BeTrue();
        (friend < colleague).Should().BeTrue();
        (colleague < other).Should().BeTrue();
        (family == ContactRelationType.Family).Should().BeTrue();
        (family != friend).Should().BeTrue();
    }
}