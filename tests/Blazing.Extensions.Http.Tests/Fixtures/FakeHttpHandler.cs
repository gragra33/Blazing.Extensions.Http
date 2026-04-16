namespace Blazing.Extensions.Http.Tests.Fixtures;

/// <summary>
/// An <see cref="HttpMessageHandler"/> test double that delegates each request to a
/// user-supplied function, enabling deterministic response control in integration tests.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    internal FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_respond(request));
    }
}
