namespace Blazing.Extensions.Http.Tests.Fixtures;

/// <summary>
/// A <see cref="Stream"/> that returns a fixed block of initial bytes then deterministically
/// triggers cancellation. On the read after the initial data is exhausted, the stream calls
/// <see cref="CancellationTokenSource.CancelAsync"/> on the supplied source before
/// awaiting an infinite delay on the <see cref="CancellationToken"/> passed to
/// <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>. Because cancelling the source
/// also signals that token, the delay throws <see cref="OperationCanceledException"/> immediately.
/// This gives tests a deterministic, timing-independent way to simulate a mid-stream cancellation.
/// </summary>
internal sealed class CancellationStream : Stream
{
    private readonly byte[] _initialData;
    private readonly CancellationTokenSource _cts;
    private int _position;

    internal CancellationStream(byte[] initialData, CancellationTokenSource cts)
    {
        _initialData = initialData;
        _cts = cts;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position < _initialData.Length)
        {
            int count = Math.Min(buffer.Length, _initialData.Length - _position);
            _initialData.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        // All initial data consumed — cancel the source, then block.
        // Task.Delay throws OperationCanceledException immediately because the token is already cancelled.
        await _cts.CancelAsync().ConfigureAwait(false);
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return 0; // Never reached
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
