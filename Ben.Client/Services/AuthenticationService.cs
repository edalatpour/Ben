using Microsoft.Identity.Client;
using CommunityToolkit.Datasync.Client.Authentication;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Bennie.Data;

namespace Bennie.Services;

public class AuthenticationService
{
    private readonly IPublicClientApplication _pca;
    private string ClientId = Constants.ApplicationId; // "d5a4dd1f-e90b-4c48-8031-15041bd3c02c"; // TODO: Replace with actual client ID
    private readonly string[] _apiScopes = Constants.ApiScopes;
    private readonly string[] _graphScopes = Constants.GraphScopes;

    public event EventHandler? AuthenticationStateChanged;

    private const string AuthStateKey = "IsAuthenticated";
    private const string UserEmailKey = "UserEmail";
    private const string UserNameKey = "UserName";
    private const string ProfilePicturePathKey = "ProfilePicturePath";

    private static readonly string ProfilePictureFilePath = Path.Combine(FileSystem.AppDataDirectory, "profile_picture.jpg");
    private static readonly HttpClient _httpClient = new();

    public AuthenticationService()
    {
        // Build the public client application
        var builder = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, Constants.AuthorityTenant);

#if WINDOWS
        // Windows: Use OS browser (system default) with loopback redirect URI
        builder = builder
            .WithRedirectUri("http://localhost");
#elif IOS || MACCATALYST
        // iOS/macOS: Use custom redirect URI
        builder = builder
            .WithRedirectUri("msauth.com.edalatpour.Ben://auth")
            .WithIosKeychainSecurityGroup("com.edalatpour.Ben");
#elif ANDROID
        // Android: Use custom redirect URI
        builder = builder
            .WithRedirectUri("msauth://com.edalatpour.ben/YOUR_SIGNATURE_HASH"); // TODO: Replace with actual signature hash
#endif

        _pca = builder.Build();

        // Enable token cache serialization for persistence
        TokenCacheHelper.EnableSerialization(_pca.UserTokenCache);
    }

    public bool IsAuthenticated
    {
        get => Preferences.Default.Get(AuthStateKey, false);
        private set => Preferences.Default.Set(AuthStateKey, value);
    }

    public string? UserEmail
    {
        get => Preferences.Default.Get(UserEmailKey, (string?)null);
        private set
        {
            if (value != null)
                Preferences.Default.Set(UserEmailKey, value);
            else
                Preferences.Default.Remove(UserEmailKey);
        }
    }

    public string? UserName
    {
        get => Preferences.Default.Get(UserNameKey, (string?)null);
        private set
        {
            if (value != null)
                Preferences.Default.Set(UserNameKey, value);
            else
                Preferences.Default.Remove(UserNameKey);
        }
    }

    public string? ProfilePicturePath
    {
        get => Preferences.Default.Get(ProfilePicturePathKey, (string?)null);
        private set
        {
            if (value != null)
                Preferences.Default.Set(ProfilePicturePathKey, value);
            else
                Preferences.Default.Remove(ProfilePicturePathKey);
        }
    }

    public async Task<AuthenticationToken> GetAuthenticationTokenAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _pca.GetAccountsAsync();
        AuthenticationResult? result = null;
        try
        {
            result = await _pca.AcquireTokenSilent(_apiScopes, accounts.FirstOrDefault()).ExecuteAsync(cancellationToken);
            UpdateAuthState(result);
        }
        catch (MsalUiRequiredException)
        {
            result = await _pca.AcquireTokenInteractive(_apiScopes).ExecuteAsync(cancellationToken);
            UpdateAuthState(result);
        }
        return new AuthenticationToken
        {
            DisplayName = result?.Account?.Username ?? "",
            ExpiresOn = result?.ExpiresOn ?? DateTimeOffset.MinValue,
            Token = result?.AccessToken ?? "",
            UserId = result?.Account?.Username ?? ""
        };
    }

    public async Task<AuthenticationResult?> SignInAsync()
    {
        try
        {
            // Try to acquire token silently first
            var accounts = await _pca.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            if (firstAccount != null)
            {
                try
                {
                    var result = await _pca.AcquireTokenSilent(_apiScopes, firstAccount)
                        .ExecuteAsync();

                    UpdateAuthState(result);
#if DEBUG
                    Console.WriteLine($"[Auth] API token account: Username={result.Account?.Username}, HomeAccountId={result.Account?.HomeAccountId?.Identifier}");
#endif
                    // Fetch profile picture if not already cached
                    if (ProfilePicturePath == null || !File.Exists(ProfilePicturePath))
                        _ = FetchAndStoreProfilePictureAsync(allowInteractiveTokenAcquisition: true, account: result.Account);
                    return result;
                }
                catch (MsalUiRequiredException)
                {
                    // Silent acquisition failed, fall through to interactive
                }
            }

            // Acquire token interactively
            var authResult = await _pca.AcquireTokenInteractive(_apiScopes)
                .ExecuteAsync();

            UpdateAuthState(authResult);
#if DEBUG
            Console.WriteLine($"[Auth] API token account: Username={authResult.Account?.Username}, HomeAccountId={authResult.Account?.HomeAccountId?.Identifier}");
#endif
            _ = FetchAndStoreProfilePictureAsync(allowInteractiveTokenAcquisition: true, account: authResult.Account);
            return authResult;
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Authentication error: {ex.Message}");
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            var accounts = await _pca.GetAccountsAsync();

            // Remove all accounts
            foreach (var account in accounts)
            {
                await _pca.RemoveAsync(account);
            }

            // Clear stored preferences
            IsAuthenticated = false;
            UserEmail = null;
            UserName = null;
            // Clean up cached profile picture
            if (File.Exists(ProfilePictureFilePath))
                File.Delete(ProfilePictureFilePath);
            ProfilePicturePath = null;
            AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sign out error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sign out with full cleanup for multi-user scenarios.
    /// Cancels sync, disposes Datasync client, and deletes local database to prevent data leakage.
    /// </summary>
    /// <param name="syncService">The sync service to cancel and dispose</param>
    /// <param name="dbContext">The planner database context to delete</param>
    /// <param name="schemaDbContext">The local schema database context sharing the same SQLite file</param>
    /// <returns>True if sign out succeeded, False if cleanup failed</returns>
    public async Task<bool> SignOutWithCleanupAsync(
        DatasyncSyncService syncService,
        PlannerDbContext dbContext,
        LocalSchemaDbContext schemaDbContext)
    {
        try
        {
            // Step 1: Cancel any in-progress sync and dispose resources
            Console.WriteLine("Signing out: Canceling sync operations");
            await syncService.CancelAndDisposeAsync();

            // Step 2: Delete local database to ensure complete data isolation
            Console.WriteLine("Signing out: Deleting local database");
            dbContext.ChangeTracker.Clear();
            schemaDbContext.ChangeTracker.Clear();
            var plannerConnection = dbContext.Database.GetDbConnection();
            if (plannerConnection.State != System.Data.ConnectionState.Closed)
            {
                plannerConnection.Close();
            }

            var schemaConnection = schemaDbContext.Database.GetDbConnection();
            if (schemaConnection.State != System.Data.ConnectionState.Closed)
            {
                schemaConnection.Close();
            }

            bool dbDeleted = await dbContext.DeleteDatabaseFileAsync();
            if (!dbDeleted)
            {
                Console.WriteLine("Warning: Database file deletion failed, but proceeding with sign-out");
            }

            // Step 3: Clear MSAL tokens and authentication state
            Console.WriteLine("Signing out: Clearing authentication state");
            await SignOutAsync();

            Console.WriteLine("Sign out completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sign out error: {ex.Message}");
            await ShowAlertAsync(
                "Error",
                $"Sign out failed: {ex.Message}",
                "OK");
            return false;
        }
    }

    /// <summary>
    /// Sign out while preserving local data. Unsynced changes remain on device for syncing on next login.
    /// </summary>
    /// <param name="syncService">The sync service reference (used to check for unsynced changes)</param>
    /// <returns>True if sign out succeeded, False if user canceled</returns>
    public async Task<bool> SignOutWithCleanupAsync(DatasyncSyncService syncService)
    {
        try
        {
            // Check if there are unsynced changes to warn user
            var hasUnsyncedChanges = await syncService.HasUnsyncedChangesAsync();

            if (hasUnsyncedChanges)
            {
                // Warn user that unsynced changes will remain locally
                await ShowAlertAsync(
                    "Unsynced Changes",
                    "You have changes that haven't been synced to the cloud yet. They will remain on this device and will be synced when you sign in again.",
                    "OK");
            }

            // Sign out (this clears authentication state but preserves local data)
            await SignOutAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sign out error: {ex.Message}");
            await ShowAlertAsync(
                "Error",
                $"Sign out failed: {ex.Message}",
                "OK");
            return false;
        }
    }

    private void UpdateAuthState(AuthenticationResult result)
    {
        IsAuthenticated = true;
        UserEmail = result.Account?.Username;
        UserName = result.Account?.Username?.Split('@')[0];
        AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<string?> AcquireGraphAccessTokenAsync(bool allowInteractiveTokenAcquisition, IAccount? account)
    {
        try
        {
            var targetAccount = account;
            if (targetAccount == null)
            {
                var accounts = await _pca.GetAccountsAsync();
                targetAccount = accounts.FirstOrDefault();
            }

            if (targetAccount == null)
            {
                return null;
            }

            try
            {
                var graphResult = await _pca.AcquireTokenSilent(_graphScopes, targetAccount).ExecuteAsync();
#if DEBUG
                LogGraphTokenDetails(graphResult.AccessToken, targetAccount, "silent");
#endif
                return graphResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                if (!allowInteractiveTokenAcquisition)
                {
                    return null;
                }

                var graphResult = await _pca.AcquireTokenInteractive(_graphScopes)
                    .WithAccount(targetAccount)
                    .ExecuteAsync();
#if DEBUG
                LogGraphTokenDetails(graphResult.AccessToken, targetAccount, "interactive");
#endif
                return graphResult.AccessToken;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to acquire Graph token: {ex.Message}");
            return null;
        }
    }

    private async Task FetchAndStoreProfilePictureAsync(bool allowInteractiveTokenAcquisition, IAccount? account)
    {
        try
        {
            var accessToken = await AcquireGraphAccessTokenAsync(allowInteractiveTokenAcquisition, account);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/photo/$value");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var notFoundBody = await response.Content.ReadAsStringAsync();
#if DEBUG
                Console.WriteLine($"[Graph] Photo response 404 for account {account?.Username}. Body: {notFoundBody}");
#endif
                var graphErrorCode = TryGetGraphErrorCode(notFoundBody);
                if (string.Equals(graphErrorCode, "ErrorNonExistentStorage", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Graph photo unavailable: backing storage/mailbox is not provisioned for this user in the current tenant context.");
                }

#if DEBUG
                Console.WriteLine($"[Graph] Photo error code: {graphErrorCode ?? "(none)"}");
#endif
                // Graph returns 404 when a user has no profile photo.
                if (File.Exists(ProfilePictureFilePath))
                {
                    File.Delete(ProfilePictureFilePath);
                }
                ProfilePicturePath = null;
                AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(ProfilePictureFilePath, imageBytes);
                ProfilePicturePath = ProfilePictureFilePath;
                AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to fetch profile picture. Status: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch profile picture: {ex.Message}");
        }
    }

    private static void LogGraphTokenDetails(string accessToken, IAccount? account, string acquisitionMode)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                Console.WriteLine("[GraphToken] Token format is not JWT.");
                return;
            }

            var payload = ParseJwtPayload(parts[1]);
            payload.TryGetProperty("aud", out var aud);
            payload.TryGetProperty("tid", out var tid);
            payload.TryGetProperty("oid", out var oid);
            payload.TryGetProperty("scp", out var scp);
            payload.TryGetProperty("upn", out var upn);
            payload.TryGetProperty("preferred_username", out var preferredUsername);

            Console.WriteLine($"[GraphToken] Mode={acquisitionMode}, Account.Username={account?.Username}, Account.HomeAccountId={account?.HomeAccountId?.Identifier}, aud={aud.GetString()}, tid={tid.GetString()}, oid={oid.GetString()}, scp={scp.GetString()}, upn={upn.GetString()}, preferred_username={preferredUsername.GetString()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphToken] Failed to decode token payload: {ex.Message}");
        }
    }

    private static JsonElement ParseJwtPayload(string base64UrlPayload)
    {
        var padded = base64UrlPayload.Replace('-', '+').Replace('_', '/');
        var mod4 = padded.Length % 4;
        if (mod4 > 0)
        {
            padded = padded.PadRight(padded.Length + (4 - mod4), '=');
        }

        var bytes = Convert.FromBase64String(padded);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    private static string? TryGetGraphErrorCode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out var code))
            {
                return code.GetString();
            }
        }
        catch
        {
            // Ignore malformed response bodies.
        }

        return null;
    }

    private static Task ShowAlertAsync(string title, string message, string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return Task.CompletedTask;
        }

        return page.DisplayAlertAsync(title, message, cancel);
    }
}

// Token cache helper for persistence
internal static class TokenCacheHelper
{
    private static readonly string CacheFilePath = Path.Combine(FileSystem.AppDataDirectory, "msalcache.bin");
    private static readonly object _fileLock = new object();

    public static void EnableSerialization(ITokenCache tokenCache)
    {
        if (OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsAndroid())
        {
            return;
        }

        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (_fileLock)
        {
            if (File.Exists(CacheFilePath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(CacheFilePath);
                    args.TokenCache.DeserializeMsalV3(data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading token cache: {ex.Message}");
                }
            }
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        // If the access operation resulted in a cache update
        if (args.HasStateChanged)
        {
            lock (_fileLock)
            {
                try
                {
                    byte[] data = args.TokenCache.SerializeMsalV3();
                    File.WriteAllBytes(CacheFilePath, data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing token cache: {ex.Message}");
                }
            }
        }
    }
}
