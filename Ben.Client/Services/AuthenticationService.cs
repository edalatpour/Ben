using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Ben.Services;

/// <summary>
/// Manages authentication state and access tokens for multiple identity providers:
/// Microsoft (personal and work/school accounts via MSAL), Google, and Apple.
/// Auth choice is persisted across launches using Preferences and SecureStorage.
/// </summary>
public class AuthenticationService
{
    // TODO: Replace with your Azure AD app registration client ID.
    // Register at https://portal.azure.com -> Azure Active Directory -> App registrations.
    // Set Supported account types to "Accounts in any organizational directory and personal Microsoft accounts".
    private const string MicrosoftClientId = "d5a4dd1f-e90b-4c48-8031-15041bd3c02c";

    private const string GoogleClientId = "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com";

    // TODO: Replace with your server-side Apple Sign In callback URL.
    // This URL must be registered as a Return URL in your Apple Services ID configuration.
    private const string AppleServicesId = "MY_APPLE_SERVICES_ID";
    private const string AppleCallbackUrl = "https://your-server.example.com/auth/apple/callback";

    // Redirect URI for MSAL callback (platform-specific).
    // Windows desktop requires loopback redirect URI; mobile platforms use custom scheme.
#if WINDOWS
    private const string MicrosoftRedirectUri = "http://localhost";
#else
    private const string MicrosoftRedirectUri = $"msal{MicrosoftClientId}://auth";
#endif

    // Scopes requested from Microsoft identity platform.
    private static readonly string[] MicrosoftScopes = ["openid", "profile", "email", "offline_access"];

    // Preferences key that stores which provider the user last used (or "skipped").
    private const string PrefsKeyProvider = "auth_provider";

    // SecureStorage keys for non-MSAL tokens (MSAL manages its own cache).
    private const string SecureKeyGoogleToken = "auth_google_token";
    private const string SecureKeyAppleToken = "auth_apple_token";

    // Provider name constants stored in Preferences.
    private const string ProviderNone = "none";
    private const string ProviderMicrosoft = "microsoft";
    private const string ProviderGoogle = "google";
    private const string ProviderApple = "apple";
    private const string ProviderSkipped = "skipped";

    private readonly IPublicClientApplication _msalClient;
    private readonly ILogger<AuthenticationService> _logger;

    private string? _accessToken;

    public AuthenticationService(ILogger<AuthenticationService> logger)
    {
        _logger = logger;
        _msalClient = PublicClientApplicationBuilder
            .Create(MicrosoftClientId)
            // "common" accepts both personal Microsoft accounts and work/school accounts.
            .WithAuthority(AzureCloudInstance.AzurePublic, "common")
            .WithRedirectUri(MicrosoftRedirectUri)
#if IOS || MACCATALYST
            .WithParentActivityOrWindow(() => Platform.GetCurrentUIViewController())
#elif WINDOWS
            .WithParentActivityOrWindow(() =>
                Application.Current?.Windows.Count > 0
                    ? Application.Current.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                    : null)
#endif
            .Build();
    }

    /// <summary>Gets the current access token, or null if the user is not authenticated.</summary>
    public string? AccessToken => _accessToken;

    /// <summary>Gets whether the user is currently authenticated with a valid token.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    /// <summary>
    /// Attempts to restore a previous session without showing UI.
    /// Returns <c>true</c> if the session was restored (authenticated) OR if the user previously
    /// chose to skip sign-in (offline-only mode). Returns <c>false</c> only on first launch or
    /// after sign-out, meaning the login screen should be shown.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        string provider = Preferences.Default.Get(PrefsKeyProvider, ProviderNone);

        return provider switch
        {
            ProviderMicrosoft => await TryRestoreMicrosoftSessionAsync(),
            ProviderGoogle => await TryRestoreStoredTokenAsync(SecureKeyGoogleToken),
            ProviderApple => await TryRestoreStoredTokenAsync(SecureKeyAppleToken),
            // User explicitly skipped sign-in; allow straight into the app (offline mode).
            ProviderSkipped => true,
            // First launch or after sign-out – show login page.
            _ => false
        };
    }

    /// <summary>
    /// Signs in with a Microsoft account (personal or work/school) using MSAL.
    /// Attempts a silent token acquisition first; falls back to interactive sign-in.
    /// </summary>
    /// <returns>True if sign-in succeeded.</returns>
    public async Task<bool> SignInWithMicrosoftAsync()
    {
        try
        {
            AuthenticationResult result;

            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            try
            {
                // Try silent first to avoid prompting the user unnecessarily.
                result = await _msalClient
                    .AcquireTokenSilent(MicrosoftScopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                // Silent failed – show the interactive sign-in prompt.
                result = await _msalClient
                    .AcquireTokenInteractive(MicrosoftScopes)
                    .ExecuteAsync();
            }

            // Use the id_token so the server can validate the issuer and user identity.
            _accessToken = result.IdToken ?? result.AccessToken;
            Preferences.Default.Set(PrefsKeyProvider, ProviderMicrosoft);
            return true;
        }
        catch (MsalException e)
        {
            Debug.WriteLine($"MSAL error during Microsoft sign-in: {e.Message}");
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Signs in with a Google account using the platform WebAuthenticator (OAuth 2.0 / PKCE).
    /// The resulting id_token is a JWT the server can validate against Google's public keys.
    /// </summary>
    /// <returns>True if sign-in succeeded.</returns>
    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            // Build the Google OAuth 2.0 authorization URL.
            // TODO: Update the redirect_uri to match what you registered in the Google Cloud Console.
            string callbackScheme = "com.edalatpour.ben";
            string callbackUrl = $"{callbackScheme}:/oauth2redirect";

            string state = Guid.NewGuid().ToString("N");
            string nonce = Guid.NewGuid().ToString("N");

            var authUrl = new Uri(
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(GoogleClientId)}" +
                "&response_type=token+id_token" +
                $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
                "&scope=openid+profile+email" +
                $"&state={state}" +
                $"&nonce={nonce}");

            var callbackUri = new Uri(callbackUrl);

            WebAuthenticatorResult result = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions
                {
                    Url = authUrl,
                    CallbackUrl = callbackUri,
                    PrefersEphemeralWebBrowserSession = true
                });

            // Extract the id_token from the callback fragment.
            if (result.Properties.TryGetValue("id_token", out string? idToken) && !string.IsNullOrEmpty(idToken))
            {
                _accessToken = idToken;
                Preferences.Default.Set(PrefsKeyProvider, ProviderGoogle);
                await SecureStorage.Default.SetAsync(SecureKeyGoogleToken, idToken);
                return true;
            }

            return false;
        }
        catch (TaskCanceledException)
        {
            // User cancelled the sign-in flow.
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Signs in with Apple ID.
    /// On iOS and macOS uses the native AppleSignInAuthenticator; falls back to
    /// WebAuthenticator on other platforms.
    /// </summary>
    /// <returns>True if sign-in succeeded.</returns>
    public async Task<bool> SignInWithAppleAsync()
    {
        try
        {
#if IOS || MACCATALYST
            var request = new AppleSignInAuthenticator.Options
            {
                IncludeEmailScope = true,
                IncludeFullNameScope = true
            };

            WebAuthenticatorResult result = await AppleSignInAuthenticator.Default.AuthenticateAsync(request);

            if (result.Properties.TryGetValue("id_token", out string? idToken) && !string.IsNullOrEmpty(idToken))
            {
                _accessToken = idToken;
                Preferences.Default.Set(PrefsKeyProvider, ProviderApple);
                await SecureStorage.Default.SetAsync(SecureKeyAppleToken, idToken);
                return true;
            }

            return false;
#else
            // Fallback for non-Apple platforms: use web-based Sign in with Apple.
            string state = Guid.NewGuid().ToString("N");
            string nonce = Guid.NewGuid().ToString("N");

            var authUrl = new Uri(
                "https://appleid.apple.com/auth/authorize" +
                $"?client_id={Uri.EscapeDataString(AppleServicesId)}" +
                "&response_type=code+id_token" +
                "&response_mode=form_post" +
                $"&redirect_uri={Uri.EscapeDataString(AppleCallbackUrl)}" +
                "&scope=name+email" +
                $"&state={state}" +
                $"&nonce={nonce}");

            WebAuthenticatorResult result = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions
                {
                    Url = authUrl,
                    CallbackUrl = new Uri(AppleCallbackUrl),
                    PrefersEphemeralWebBrowserSession = true
                });

            if (result.Properties.TryGetValue("id_token", out string? idToken) && !string.IsNullOrEmpty(idToken))
            {
                _accessToken = idToken;
                Preferences.Default.Set(PrefsKeyProvider, ProviderApple);
                await SecureStorage.Default.SetAsync(SecureKeyAppleToken, idToken);
                return true;
            }

            return false;
#endif
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Records that the user chose to continue without signing in.
    /// The app will work offline; this preference is remembered across launches.
    /// </summary>
    public void MarkAsSkipped()
    {
        Preferences.Default.Set(PrefsKeyProvider, ProviderSkipped);
    }

    /// <summary>
    /// Signs out the current user, clears all stored tokens and preferences, and stops sync.
    /// </summary>
    public async Task SignOutAsync()
    {
        _accessToken = null;
        Preferences.Default.Remove(PrefsKeyProvider);

        try
        {
            SecureStorage.Default.Remove(SecureKeyGoogleToken);
            SecureStorage.Default.Remove(SecureKeyAppleToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove tokens from SecureStorage during sign-out.");
        }

        // Remove all cached MSAL accounts.
        var accounts = await _msalClient.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _msalClient.RemoveAsync(account);
        }
    }

    /// <summary>
    /// Attempts to silently restore a Microsoft session from MSAL's token cache.
    /// Clears the saved provider preference if the cache has expired or is empty.
    /// </summary>
    private async Task<bool> TryRestoreMicrosoftSessionAsync()
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                Preferences.Default.Remove(PrefsKeyProvider);
                return false;
            }

            var result = await _msalClient
                .AcquireTokenSilent(MicrosoftScopes, account)
                .ExecuteAsync();

            _accessToken = result.IdToken ?? result.AccessToken;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Silent Microsoft token acquisition failed; user will be prompted to sign in.");
            Preferences.Default.Remove(PrefsKeyProvider);
            return false;
        }
    }

    /// <summary>
    /// Restores a Google or Apple token from SecureStorage.
    /// </summary>
    private async Task<bool> TryRestoreStoredTokenAsync(string secureKey)
    {
        try
        {
            string? token = await SecureStorage.Default.GetAsync(secureKey);
            if (!string.IsNullOrEmpty(token))
            {
                _accessToken = token;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve token from SecureStorage (key: {Key}); user will be prompted to sign in.", secureKey);
        }

        Preferences.Default.Remove(PrefsKeyProvider);
        return false;
    }
}
