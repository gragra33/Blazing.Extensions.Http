namespace Blazing.Extensions.Http.Tests.Models;

public class BitUnitTests
{
    [Theory]
    [InlineData(BitUnit.b, "b")]
    [InlineData(BitUnit.Kb, "Kb")]
    [InlineData(BitUnit.Mb, "Mb")]
    [InlineData(BitUnit.Gb, "Gb")]
    [InlineData(BitUnit.Tb, "Tb")]
    public void BitUnit_ShouldHaveCorrectValues(BitUnit unit, string expectedName)
    {
        // Act
        var name = unit.ToString();

        // Assert
        name.Should().Be(expectedName);
    }

    [Fact]
    public void BitUnit_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<BitUnit>();

        // Assert
        values.Should().Contain(BitUnit.b);
        values.Should().Contain(BitUnit.Kb);
        values.Should().Contain(BitUnit.Mb);
        values.Should().Contain(BitUnit.Gb);
        values.Should().Contain(BitUnit.Tb);
    }
}
