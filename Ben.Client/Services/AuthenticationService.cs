using Microsoft.Identity.Client;

namespace Ben.Services;

public class AuthenticationService
{
    private readonly IPublicClientApplication _pca;
    private const string ClientId = "d5a4dd1f-e90b-4c48-8031-15041bd3c02c"; // TODO: Replace with actual client ID
    private readonly string[] _scopes = new[] { "User.Read" };
    
    public event EventHandler? AuthenticationStateChanged;
    
    private const string AuthStateKey = "IsAuthenticated";
    private const string UserEmailKey = "UserEmail";
    private const string UserNameKey = "UserName";

    public AuthenticationService()
    {
        // Build the public client application
        var builder = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "common");

#if WINDOWS
        // Windows: Use OS browser (system default) with loopback redirect URI
        builder = builder
            .WithRedirectUri("http://localhost");
#elif IOS || MACCATALYST
        // iOS/macOS: Use custom redirect URI
        builder = builder
            .WithRedirectUri("msauth.com.edalatpour.ben://auth")
            .WithIosKeychainSecurityGroup("com.microsoft.adalcache");
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
                    var result = await _pca.AcquireTokenSilent(_scopes, firstAccount)
                        .ExecuteAsync();
                    
                    UpdateAuthState(result);
                    return result;
                }
                catch (MsalUiRequiredException)
                {
                    // Silent acquisition failed, fall through to interactive
                }
            }

            // Acquire token interactively
            var authResult = await _pca.AcquireTokenInteractive(_scopes)
                .ExecuteAsync();

            UpdateAuthState(authResult);
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
            AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sign out error: {ex.Message}");
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
                await Application.Current!.MainPage!.DisplayAlert(
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
            await Application.Current!.MainPage!.DisplayAlert(
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
}

// Token cache helper for persistence
internal static class TokenCacheHelper
{
    private static readonly string CacheFilePath = Path.Combine(FileSystem.AppDataDirectory, "msalcache.bin");
    private static readonly object _fileLock = new object();

    public static void EnableSerialization(ITokenCache tokenCache)
    {
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
