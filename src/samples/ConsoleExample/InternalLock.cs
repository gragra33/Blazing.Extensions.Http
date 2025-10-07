/// <summary>
/// Provides a simple lock mechanism using a semaphore for synchronizing console output or other critical sections.
/// </summary>
internal static class InternalLock
{
    /// <summary>
    /// The semaphore used for locking.
    /// </summary>
    private static readonly SemaphoreSlim _semaphore = new(initialCount: 1);

    /// <summary>
    /// Acquires the lock, blocking if necessary.
    /// </summary>
    public static void Wait()
        => _semaphore.Wait();

    /// <summary>
    /// Releases the lock.
    /// </summary>
    public static void Release()
        => _semaphore.Release();

    /// <summary>
    /// Gets a value indicating whether the lock is currently held.
    /// </summary>
    public static bool IsLocked
        => _semaphore.CurrentCount > 0;
}