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
    private readonly IAuthService _authService;
    private readonly ILogger<DatasyncSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly object _startLock = new();
    private bool _started;
    private bool _disposed;

    public DatasyncSyncService(
        IServiceScopeFactory scopeFactory,
        IConnectivity connectivity,
        DatasyncOptions options,
        IAuthService authService,
        ILogger<DatasyncSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectivity = connectivity;
        _options = options;
        _authService = authService;
        _logger = logger;
    }

    public void Start()
    {
        lock (_startLock)
        {
            if (_started)
            {
                return;
            }

            if (_options.Endpoint == null)
            {
                return;
            }

            // Do not sync when the user is not authenticated (local-only mode).
            if (!_authService.IsAuthenticated)
            {
                return;
            }

            _started = true;
            _connectivity.ConnectivityChanged += OnConnectivityChanged;
        }

        _ = TrySyncAsync();
    }

    /// <summary>
    /// Stops and restarts the sync service. Call this after the user signs in.
    /// </summary>
    public void Restart()
    {
        lock (_startLock)
        {
            if (_started)
            {
                _connectivity.ConnectivityChanged -= OnConnectivityChanged;
                _started = false;
            }
        }

        Start();
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
