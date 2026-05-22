using Ben.Data;
using Ben.Services;

namespace Ben.Services.Auth;

public sealed class AuthenticationLifecycleCoordinator : IAuthenticationLifecycleCoordinator
{
    private const string PendingSignOutResetKey = "AuthLifecycle.PendingSignOutReset";

    private readonly DatasyncSyncService _syncService;
    private readonly PlannerDbContext _plannerDbContext;
    private readonly ILocalDatabaseLifecycleService _localDatabaseLifecycleService;
    private readonly MsalService _authenticationService;
    private readonly ExternalIdAuthService _externalIdAuthService;

    public AuthenticationLifecycleCoordinator(
        DatasyncSyncService syncService,
        PlannerDbContext plannerDbContext,
        ILocalDatabaseLifecycleService localDatabaseLifecycleService,
        MsalService authenticationService,
        ExternalIdAuthService externalIdAuthService)
    {
        _syncService = syncService;
        _plannerDbContext = plannerDbContext;
        _localDatabaseLifecycleService = localDatabaseLifecycleService;
        _authenticationService = authenticationService;
        _externalIdAuthService = externalIdAuthService;
    }

    public async Task<bool> InitializeSignedInUserAsync()
    {
        try
        {
            bool dbCreated = await _localDatabaseLifecycleService.CreateLocalDatabaseAsync().ConfigureAwait(false);
            if (!dbCreated)
            {
                Console.WriteLine("Warning: Local database creation failed, but proceeding with sync initialization.");
            }

            bool clientInitialized = await _plannerDbContext.ReinitializeDatasyncClientAsync().ConfigureAwait(false);
            if (!clientInitialized)
            {
                Console.WriteLine("Warning: Datasync client reinitialization failed.");
            }

            _syncService.Start();
            _ = _syncService.TrySyncNowAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication lifecycle initialization failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignOutWithCleanupAsync()
    {
        try
        {
            // If the app closes mid-sign-out, startup uses this marker to force a clean DB reset.
            Preferences.Default.Set(PendingSignOutResetKey, true);

            await _syncService.CancelAndDisposeAsync().ConfigureAwait(false);

            bool dbReset = await _localDatabaseLifecycleService.DeleteAndRecreateLocalDatabaseAsync().ConfigureAwait(false);
            if (!dbReset)
            {
                Console.WriteLine("Warning: Local database reset was not fully successful during sign-out.");
            }

            if (_authenticationService.IsAuthenticated)
            {
                await _authenticationService.SignOutAsync().ConfigureAwait(false);
            }

            if (_externalIdAuthService.IsAuthenticated)
            {
                _externalIdAuthService.SignOut();
            }

            Preferences.Default.Set(PendingSignOutResetKey, false);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication lifecycle sign-out failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignOutAsync()
    {
        try
        {
            await _syncService.CancelAndDisposeAsync().ConfigureAwait(false);

            if (_authenticationService.IsAuthenticated)
            {
                await _authenticationService.SignOutAsync().ConfigureAwait(false);
            }

            if (_externalIdAuthService.IsAuthenticated)
            {
                _externalIdAuthService.SignOut();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication lifecycle sign-out failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResetLocalDataAsync()
    {
        try
        {
            await _syncService.CancelAndDisposeAsync().ConfigureAwait(false);

            bool dbReset = await _localDatabaseLifecycleService.DeleteAndRecreateLocalDatabaseAsync().ConfigureAwait(false);
            if (!dbReset)
            {
                Console.WriteLine("Warning: Local database reset was not fully successful.");
            }

            if (!_authenticationService.IsAuthenticated && !_externalIdAuthService.IsAuthenticated)
            {
                return dbReset;
            }

            bool clientInitialized = await _plannerDbContext.ReinitializeDatasyncClientAsync().ConfigureAwait(false);
            if (!clientInitialized)
            {
                Console.WriteLine("Warning: Datasync client reinitialization failed after local data reset.");
            }

            _syncService.Start();
            _ = _syncService.TrySyncNowAsync();

            return dbReset && clientInitialized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication lifecycle local data reset failed: {ex.Message}");
            return false;
        }
    }
}
