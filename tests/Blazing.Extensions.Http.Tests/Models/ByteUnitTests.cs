namespace Blazing.Extensions.Http.Tests.Models;

public class ByteUnitTests
{
    [Theory]
    [InlineData(ByteUnit.B, "B")]
    [InlineData(ByteUnit.KiB, "KiB")]
    [InlineData(ByteUnit.MiB, "MiB")]
    [InlineData(ByteUnit.GiB, "GiB")]
    [InlineData(ByteUnit.TiB, "TiB")]
    public void ByteUnit_ShouldHaveCorrectValues(ByteUnit unit, string expectedName)
    {
        // Act
        var name = unit.ToString();

        // Assert
        name.Should().Be(expectedName);
    }

    [Fact]
    public void ByteUnit_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<ByteUnit>();

        // Assert
        values.Should().Contain(ByteUnit.B);
        values.Should().Contain(ByteUnit.KiB);
        values.Should().Contain(ByteUnit.MiB);
        values.Should().Contain(ByteUnit.GiB);
        values.Should().Contain(ByteUnit.TiB);
    }
}
