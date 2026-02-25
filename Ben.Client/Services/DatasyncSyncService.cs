using CommunityToolkit.Datasync.Client.Offline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Ben.Data;

namespace Ben.Services;

public sealed class DatasyncSyncService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectivity _connectivity;
    private readonly AuthenticationService _authService;
    private readonly DatasyncOptions _options;
    private readonly ILogger<DatasyncSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _started;
    private bool _disposed;

    public event EventHandler? SyncStarted;
    public event EventHandler? SyncCompleted;

    public DatasyncSyncService(
        IServiceScopeFactory scopeFactory,
        IConnectivity connectivity,
        AuthenticationService authService,
        DatasyncOptions options,
        ILogger<DatasyncSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectivity = connectivity;
        _authService = authService;
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

    /// <summary>
    /// Check if there are unsynced changes in the local database
    /// </summary>
    public async Task<bool> HasUnsyncedChangesAsync()
    {
        var count = await GetUnsyncedChangesCountAsync();
        return count > 0;
    }

    /// <summary>
    /// Get the number of unsynced changes in the local database
    /// </summary>
    public async Task<int> GetUnsyncedChangesCountAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            PlannerDbContext context = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();

            // Query the Datasync operations queue to get pending changes
            var count = await context.DatasyncOperationsQueue.CountAsync();
            _logger.LogInformation("Found {Count} pending operations in queue", count);
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for unsynced changes.");
            return 0;
        }
    }

    /// <summary>
    /// Attempt to sync any pending changes
    /// </summary>
    public async Task<bool> TrySyncNowAsync()
    {
        if (!_authService.IsAuthenticated)
        {
            _logger.LogWarning("Cannot sync while not authenticated.");
            return false;
        }

        return await TrySyncAsync();
    }

    private async Task<bool> TrySyncAsync()
    {
        if (_options.Endpoint == null)
        {
            return false;
        }

        // Don't sync if user is not authenticated
        if (!_authService.IsAuthenticated)
        {
            _logger.LogInformation("Skipping sync - user is not authenticated.");
            return false;
        }

        if (_connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return false;
        }

        if (!await _syncLock.WaitAsync(0))
        {
            return false;
        }

        SyncStarted?.Invoke(this, EventArgs.Empty);

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

            return pushResult.IsSuccessful && pullResult.IsSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Datasync synchronization failed.");
            return false;
        }
        finally
        {
            _syncLock.Release();
            SyncCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
