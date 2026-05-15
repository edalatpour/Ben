using Ben.Data;
using Microsoft.EntityFrameworkCore;

namespace Ben.Services.Auth;

public sealed class AuthenticationLifecycleCoordinator : IAuthenticationLifecycleCoordinator
{
    private const string PendingSignOutResetKey = "AuthLifecycle.PendingSignOutReset";

    private readonly DatasyncSyncService _syncService;
    private readonly PlannerDbContext _plannerDbContext;
    private readonly LocalSchemaDbContext _schemaDbContext;
    private readonly AuthenticationService _authenticationService;
    private readonly ExternalIdAuthService _externalIdAuthService;

    public AuthenticationLifecycleCoordinator(
        DatasyncSyncService syncService,
        PlannerDbContext plannerDbContext,
        LocalSchemaDbContext schemaDbContext,
        AuthenticationService authenticationService,
        ExternalIdAuthService externalIdAuthService)
    {
        _syncService = syncService;
        _plannerDbContext = plannerDbContext;
        _schemaDbContext = schemaDbContext;
        _authenticationService = authenticationService;
        _externalIdAuthService = externalIdAuthService;
    }

    public async Task<bool> InitializeSignedInUserAsync()
    {
        try
        {
            bool dbRecreated = await _plannerDbContext.RecreateAndInitializeDatabaseAsync().ConfigureAwait(false);
            if (!dbRecreated)
            {
                Console.WriteLine("Warning: Database recreation failed, but proceeding with sync initialization.");
            }

            await _schemaDbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
            LocalMigrationRunner.ApplyMigrations(_schemaDbContext);

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

            _plannerDbContext.ChangeTracker.Clear();
            _schemaDbContext.ChangeTracker.Clear();

            var plannerConnection = _plannerDbContext.Database.GetDbConnection();
            if (plannerConnection.State != System.Data.ConnectionState.Closed)
            {
                plannerConnection.Close();
            }

            var schemaConnection = _schemaDbContext.Database.GetDbConnection();
            if (schemaConnection.State != System.Data.ConnectionState.Closed)
            {
                schemaConnection.Close();
            }

            bool dbDeleted = await _plannerDbContext.DeleteDatabaseFileAsync().ConfigureAwait(false);
            if (!dbDeleted)
            {
                Console.WriteLine("Warning: Database file deletion failed, but proceeding with sign-out.");
            }

            // Immediately recreate a fresh local database so signed-out users can continue
            // using the app locally without backend connectivity.
            bool dbRecreated = await _plannerDbContext.RecreateAndInitializeDatabaseAsync().ConfigureAwait(false);
            if (!dbRecreated)
            {
                Console.WriteLine("Warning: Failed to recreate local database after sign-out.");
            }

            await _schemaDbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
            LocalMigrationRunner.ApplyMigrations(_schemaDbContext);

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
}
