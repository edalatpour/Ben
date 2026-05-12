namespace Ben.Services;

public sealed class SqliteWriteCoordinator
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return _writeGate.WaitAsync(cancellationToken);
    }

    public Task<bool> TryWaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return _writeGate.WaitAsync(timeout, cancellationToken);
    }

    public void Release()
    {
        _writeGate.Release();
    }
}
