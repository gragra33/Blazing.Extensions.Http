namespace Blazing.Extensions.Http.Tests.UnitTests.Models;

[Trait("Category", "Unit")]
public class GetResultTests
{
    [Fact]
    public void Ok_WithDefaultStatusCode_ReturnsSuccess()
    {
        // Act
        var result = GetResult.Ok();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Ok_WithExplicitStatusCode_ReturnsSuccessWithGivenCode()
    {
        // Act
        var result = GetResult.Ok(HttpStatusCode.NoContent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public void Failed_WithStatusCodeAndMessage_ReturnsFailure()
    {
        // Act
        var result = GetResult.Failed(HttpStatusCode.NotFound, "Not Found: /missing");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Not Found: /missing");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failed_WithNullStatusCode_StatusCodeIsNull()
    {
        // Act
        var result = GetResult.Failed(null, "Cancelled");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().BeNull();
        result.ErrorMessage.Should().Be("Cancelled");
    }

    [Fact]
    public void Failed_WithException_CapturesException()
    {
        // Arrange
        var ex = new InvalidOperationException("network failure");

        // Act
        var result = GetResult.Failed(null, ex.Message, ex);

        // Assert
        result.Exception.Should().BeSameAs(ex);
        result.ErrorMessage.Should().Be("network failure");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Ok_IsAssignableToResultBase()
    {
        // Act
        var result = GetResult.Ok();

        // Assert
        result.Should().BeAssignableTo<ResultBase>();
    }

    [Fact]
    public void Ok_ErrorMessageAndExceptionAreNull()
    {
        // Act
        var result = GetResult.Ok(HttpStatusCode.OK);

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void Failed_VariousStatusCodes_CapturesCorrectCode(HttpStatusCode code)
    {
        // Act
        var result = GetResult.Failed(code, "error");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(code);
    }
}
