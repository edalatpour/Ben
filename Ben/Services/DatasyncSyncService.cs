using CommunityToolkit.Datasync.Client.Offline;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Ben.Data;

namespace Ben.Services;

public sealed class DatasyncSyncService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectivity _connectivity;
    private readonly DatasyncOptions _options;
    private readonly ILogger<DatasyncSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _started;
    private bool _disposed;

    public DatasyncSyncService(
        IServiceScopeFactory scopeFactory,
        IConnectivity connectivity,
        DatasyncOptions options,
        ILogger<DatasyncSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectivity = connectivity;
        _options = options;
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        if (_options.Endpoint == null)
        {
            return;
        }

        _started = true;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        _ = TrySyncAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _syncLock.Dispose();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _ = TrySyncAsync();
        }
    }

    public Task TriggerSyncAsync()
    {
        return TrySyncAsync();
    }

    private async Task TrySyncAsync()
    {
        if (_options.Endpoint == null)
        {
            return;
        }

        if (_connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        if (!await _syncLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            PlannerDbContext context = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();

            PushResult pushResult = await context.PushAsync();
            if (!pushResult.IsSuccessful)
            {
                _logger.LogWarning("Datasync push completed with {Count} failed requests.",
                    pushResult.FailedRequests.Count);
            }

            PullResult pullResult = await context.PullAsync();
            if (!pullResult.IsSuccessful)
            {
                _logger.LogWarning("Datasync pull completed with {Count} failed requests.",
                    pullResult.FailedRequests.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Datasync synchronization failed.");
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
