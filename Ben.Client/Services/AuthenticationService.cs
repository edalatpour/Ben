using Microsoft.Identity.Client;

namespace Ben.Services;

/// <summary>
/// Manages authentication state and access tokens for multiple identity providers:
/// Microsoft (personal and work/school accounts via MSAL), Google, and Apple.
/// </summary>
public class AuthenticationService
{
    // TODO: Replace with your Azure AD app registration client ID.
    // Register at https://portal.azure.com -> Azure Active Directory -> App registrations.
    // Set Supported account types to "Accounts in any organizational directory and personal Microsoft accounts".
    private const string MicrosoftClientId = "YOUR_MICROSOFT_CLIENT_ID";

    // TODO: Replace with your Google OAuth 2.0 client ID.
    // Register at https://console.cloud.google.com -> APIs & Services -> Credentials.
    private const string GoogleClientId = "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com";

    // TODO: Replace with your Apple Services ID (reverse domain notation).
    // Register at https://developer.apple.com -> Certificates, Identifiers & Profiles -> Identifiers -> Services IDs.
    private const string AppleServicesId = "YOUR_APPLE_SERVICES_ID";

    // TODO: Replace with your server-side Apple Sign In callback URL.
    // This URL must be registered as a Return URL in your Apple Services ID configuration.
    private const string AppleCallbackUrl = "https://your-server.example.com/auth/apple/callback";

    // Redirect scheme registered in Info.plist for MSAL callback.
    private const string MicrosoftRedirectUri = $"msal{MicrosoftClientId}://auth";

    // Scopes requested from Microsoft identity platform.
    private static readonly string[] MicrosoftScopes = ["openid", "profile", "email", "offline_access"];

    private readonly IPublicClientApplication _msalClient;

    private string? _accessToken;

    public AuthenticationService()
    {
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

    /// <summary>Gets whether the user is currently authenticated.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

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
            return true;
        }
        catch (MsalException)
        {
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

    /// <summary>Signs out the current user and clears stored tokens.</summary>
    public async Task SignOutAsync()
    {
        _accessToken = null;

        // Remove all cached MSAL accounts.
        var accounts = await _msalClient.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _msalClient.RemoveAsync(account);
        }
    }
}
