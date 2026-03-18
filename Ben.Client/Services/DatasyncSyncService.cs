using CommunityToolkit.Datasync.Client.Offline;
using CommunityToolkit.Datasync.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using Ben.Data;
using System.Reflection;
using System.Text;
using System.IO;

namespace Ben.Services;

public sealed class DatasyncSyncService : IDisposable
{
    private static readonly TimeSpan AutoSyncDebounce = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectivity _connectivity;
    private readonly AuthenticationService _authService;
    private readonly DatasyncOptions _options;
    private readonly ILogger<DatasyncSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly object _syncScheduleGate = new();
    private bool _started;
    private bool _disposed;
    private CancellationTokenSource? _scheduledSyncCts;
    private int _pendingCountSnapshot;

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

        CancellationTokenSource? scheduledSyncCts;
        lock (_syncScheduleGate)
        {
            scheduledSyncCts = _scheduledSyncCts;
            _scheduledSyncCts = null;
        }

        if (scheduledSyncCts != null)
        {
            scheduledSyncCts.Cancel();
            scheduledSyncCts.Dispose();
        }

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
        ScheduleSync(AutoSyncDebounce);
        return Task.CompletedTask;
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
        if (_disposed)
        {
            return _pendingCountSnapshot;
        }

        if (!await _syncLock.WaitAsync(0))
        {
            return _pendingCountSnapshot;
        }

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            PlannerDbContext context = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();

            // Query the Datasync operations queue to get pending changes
            var count = await context.DatasyncOperationsQueue.CountAsync();
            _pendingCountSnapshot = count;
            _logger.LogInformation("Found {Count} pending operations in queue", count);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for unsynced changes.");
            return _pendingCountSnapshot;
        }
        finally
        {
            _syncLock.Release();
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

    private void ScheduleSync(TimeSpan delay)
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource cts = new();
        CancellationTokenSource? previous = null;

        lock (_syncScheduleGate)
        {
            if (_disposed)
            {
                cts.Dispose();
                return;
            }

            previous = _scheduledSyncCts;
            _scheduledSyncCts = cts;
        }

        if (previous != null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        _ = RunScheduledSyncAsync(cts, delay);
    }

    private async Task RunScheduledSyncAsync(CancellationTokenSource cts, TimeSpan delay)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cts.Token);
            }

            cts.Token.ThrowIfCancellationRequested();
            await TrySyncAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_syncScheduleGate)
            {
                if (ReferenceEquals(_scheduledSyncCts, cts))
                {
                    _scheduledSyncCts = null;
                }
            }

            cts.Dispose();
        }
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

        bool shouldScheduleRetry = false;
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

                foreach (var failedRequest in pushResult.FailedRequests)
                {
                    _logger.LogWarning("Datasync push failure detail: {FailedRequest}", DescribeFailedRequest(failedRequest));
                }
            }

            int pendingAfterPush = await context.DatasyncOperationsQueue.CountAsync();
            _pendingCountSnapshot = pendingAfterPush;
            if (pendingAfterPush > 0)
            {
                _logger.LogWarning(
                    "Skipping pull because {Count} pending operations remain in queue after push.",
                    pendingAfterPush);

                await LogQueueSnapshotAsync(context);
                shouldScheduleRetry = true;
                return false;
            }

            PullResult pullResult = await context.PullAsync();
            if (!pullResult.IsSuccessful)
            {
                _logger.LogWarning("Datasync pull completed with {Count} failed requests.",
                    pullResult.FailedRequests.Count);

                foreach (var failedRequest in pullResult.FailedRequests)
                {
                    _logger.LogWarning("Datasync pull failure detail: {FailedRequest}", DescribeFailedRequest(failedRequest));
                }

                shouldScheduleRetry = true;
            }

            int pendingAfterPull = await context.DatasyncOperationsQueue.CountAsync();
            _pendingCountSnapshot = pendingAfterPull;

            if (pendingAfterPull > 0)
            {
                shouldScheduleRetry = true;
            }

            bool success = pushResult.IsSuccessful && pullResult.IsSuccessful;
            if (!success)
            {
                shouldScheduleRetry = true;
            }

            return success;
        }
        catch (DatasyncException ex)
        {
            _logger.LogError(ex, "Datasync synchronization failed with DatasyncException: {Message}", ex.Message);

            using IServiceScope diagnosticsScope = _scopeFactory.CreateScope();
            PlannerDbContext diagnosticsContext = diagnosticsScope.ServiceProvider.GetRequiredService<PlannerDbContext>();
            await LogQueueSnapshotAsync(diagnosticsContext);
            shouldScheduleRetry = await RefreshPendingCountSnapshotAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Datasync synchronization failed.");
            shouldScheduleRetry = await RefreshPendingCountSnapshotAsync();
            return false;
        }
        finally
        {
            _syncLock.Release();
            SyncCompleted?.Invoke(this, EventArgs.Empty);

            if (shouldScheduleRetry)
            {
                ScheduleSync(RetryInterval);
            }
        }
    }

    private async Task<bool> RefreshPendingCountSnapshotAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            PlannerDbContext context = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
            int count = await context.DatasyncOperationsQueue.CountAsync();
            _pendingCountSnapshot = count;
            return count > 0;
        }
        catch
        {
            return _pendingCountSnapshot > 0;
        }
    }

    private async Task LogQueueSnapshotAsync(PlannerDbContext context)
    {
        try
        {
            int totalPending = await context.DatasyncOperationsQueue.CountAsync();
            _logger.LogWarning("Datasync queue snapshot: {Count} total pending operations.", totalPending);

            var connection = context.Database.GetDbConnection();
            bool shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                List<string> columns = await GetQueueColumnsAsync(connection);
                if (columns.Count == 0)
                {
                    _logger.LogWarning("Datasync queue schema inspection returned no columns.");
                    return;
                }

                string orderColumn = columns.FirstOrDefault(static c =>
                        string.Equals(c, "Sequence", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c, "sequence", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c, "Id", StringComparison.OrdinalIgnoreCase))
                    ?? columns[0];

                string projection = string.Join(", ",
                    columns
                        .Take(12)
                        .Select(static c => $"[{c}]"));

                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    SELECT {projection}
                    FROM [DatasyncOperationsQueue]
                    ORDER BY [{orderColumn}]
                    LIMIT 10;";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new StringBuilder();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (i > 0)
                        {
                            row.Append(", ");
                        }

                        string column = reader.GetName(i);
                        string value = reader.IsDBNull(i) ? "<null>" : reader.GetValue(i)?.ToString() ?? string.Empty;
                        row.Append(column).Append('=').Append(value);
                    }

                    _logger.LogWarning("Pending operation row: {Row}", row.ToString());
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log Datasync queue snapshot.");
        }
    }

    private static async Task<List<string>> GetQueueColumnsAsync(System.Data.Common.DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('DatasyncOperationsQueue');";

        using var reader = await command.ExecuteReaderAsync();
        List<string> columns = new();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(1))
            {
                columns.Add(reader.GetString(1));
            }
        }

        return columns;
    }

    private static string DescribeFailedRequest(object failedRequest)
    {
        if (failedRequest == null)
        {
            return "<null>";
        }

        Type requestType = failedRequest.GetType();
        PropertyInfo? keyProp = requestType.GetProperty("Key");
        PropertyInfo? valueProp = requestType.GetProperty("Value");

        if (keyProp != null && valueProp != null)
        {
            object? key = SafeGetValue(keyProp, failedRequest);
            object? value = SafeGetValue(valueProp, failedRequest);
            return $"Key={key ?? "<null>"}, Value={DescribeObject(value)}{TryReadContentStream(value)}";
        }

        return DescribeObject(failedRequest);
    }

    private static string DescribeObject(object? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        Type type = value.GetType();
        PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static prop => prop.GetIndexParameters().Length == 0)
            .ToArray();

        if (props.Length == 0)
        {
            return value.ToString() ?? type.FullName ?? type.Name;
        }

        var sb = new StringBuilder();
        sb.Append(type.Name).Append(" {");

        bool first = true;
        foreach (PropertyInfo prop in props)
        {
            object? propValue = SafeGetValue(prop, value);

            if (!ShouldLogProperty(prop.Name, propValue))
            {
                continue;
            }

            if (!first)
            {
                sb.Append(", ");
            }

            first = false;
            sb.Append(prop.Name).Append('=').Append(propValue ?? "<null>");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string TryReadContentStream(object? serviceResponse)
    {
        if (serviceResponse == null)
        {
            return string.Empty;
        }

        try
        {
            PropertyInfo? streamProp = serviceResponse.GetType().GetProperty("ContentStream");
            if (streamProp == null)
            {
                return string.Empty;
            }

            if (streamProp.GetValue(serviceResponse) is not Stream stream || !stream.CanRead)
            {
                return string.Empty;
            }

            long originalPosition = 0;
            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            string content = reader.ReadToEnd();

            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            return $", ResponseBody={content}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ShouldLogProperty(string name, object? value)
    {
        if (value == null)
        {
            return true;
        }

        if (value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal or DateTime or DateTimeOffset or Guid)
        {
            return true;
        }

        if (value.GetType().IsEnum)
        {
            return true;
        }

        // Include common HTTP-ish members even when complex.
        return name.Contains("Status", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Reason", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Message", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Error", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Uri", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Content", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Request", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Method", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Header", StringComparison.OrdinalIgnoreCase);
    }

    private static object? SafeGetValue(PropertyInfo property, object source)
    {
        try
        {
            return property.GetValue(source);
        }
        catch
        {
            return "<unavailable>";
        }
    }
}
