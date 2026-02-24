using Microsoft.Identity.Client;

namespace Ben.Services;

public sealed class AuthService : IAuthService
{
    // -----------------------------------------------------------------------
    // TODO: Replace these placeholder values with real app registration IDs
    // from your Azure AD portal and Google/Apple developer consoles.
    // -----------------------------------------------------------------------

    // Azure AD / Microsoft identity platform
    // Register at https://portal.azure.com → Azure Active Directory → App registrations
    private const string MicrosoftClientId = "TODO_REPLACE_WITH_AZURE_AD_CLIENT_ID";
    private const string MicrosoftTenantId = "common"; // use tenant GUID for single-tenant
    private static readonly string[] MicrosoftScopes = ["User.Read", "offline_access"];

    // Google OAuth 2.0
    // Register at https://console.cloud.google.com → APIs & Services → Credentials
    private const string GoogleClientId = "TODO_REPLACE_WITH_GOOGLE_CLIENT_ID";
    private const string GoogleRedirectUri = "com.edalatpour.ben:/oauth2redirect";

    // Android MSAL redirect hash
    // Obtain with: keytool -exportcert -alias <alias> -keystore <keystore> | openssl sha1 -binary | openssl base64
    private const string MicrosoftAndroidSignatureHash = "TODO_REPLACE_WITH_ANDROID_SIGNATURE_HASH";

    // Preferences / SecureStorage keys
    private const string PrefsKeyIsAuthenticated = "auth_is_authenticated";
    private const string PrefsKeyIsLocalOnly = "auth_is_local_only";
    private const string PrefsKeyDisplayName = "auth_display_name";
    private const string PrefsKeyEmail = "auth_email";
    private const string PrefsKeyProvider = "auth_provider";
    private const string SecureKeyAccessToken = "auth_access_token";

    private IPublicClientApplication? _msalClient;

    public AuthService()
    {
        // Read cached state synchronously so callers can check immediately.
        IsLocalOnly = Preferences.Get(PrefsKeyIsLocalOnly, false);
        IsAuthenticated = Preferences.Get(PrefsKeyIsAuthenticated, false);
        UserDisplayName = Preferences.Get(PrefsKeyDisplayName, null as string);
        UserEmail = Preferences.Get(PrefsKeyEmail, null as string);
    }

    public bool IsAuthenticated { get; private set; }
    public bool IsLocalOnly { get; private set; }
    public string? UserDisplayName { get; private set; }
    public string? UserEmail { get; private set; }

    /// <summary>
    /// Attempts a silent token refresh for previously-authenticated accounts.
    /// Call once at startup after the UI is ready.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsAuthenticated && Preferences.Get(PrefsKeyProvider, "") == "microsoft")
        {
            await TrySilentMicrosoftSignInAsync();
        }
    }

    // ------------------------------------------------------------------
    // Microsoft Sign-In (MSAL)
    // ------------------------------------------------------------------

    public async Task<bool> SignInWithMicrosoftAsync()
    {
        try
        {
            IPublicClientApplication app = GetMsalClient();
            AuthenticationResult result;

            try
            {
                IEnumerable<IAccount> accounts = await app.GetAccountsAsync();
                result = await app
                    .AcquireTokenSilent(MicrosoftScopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                result = await app
                    .AcquireTokenInteractive(MicrosoftScopes)
                    .ExecuteAsync();
            }

            await PersistAuthAsync(
                result.ClaimsPrincipal?.Identity?.Name ?? result.Account.Username,
                result.Account.Username,
                "microsoft",
                result.AccessToken);
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

    private async Task TrySilentMicrosoftSignInAsync()
    {
        try
        {
            IPublicClientApplication app = GetMsalClient();
            IEnumerable<IAccount> accounts = await app.GetAccountsAsync();
            IAccount? account = accounts.FirstOrDefault();
            if (account == null)
            {
                ClearAuth();
                return;
            }

            AuthenticationResult result = await app
                .AcquireTokenSilent(MicrosoftScopes, account)
                .ExecuteAsync();

            await SecureStorage.SetAsync(SecureKeyAccessToken, result.AccessToken);
        }
        catch (MsalException)
        {
            ClearAuth();
        }
        catch (Exception)
        {
            // Keep existing cached state on transient failures.
        }
    }

    // ------------------------------------------------------------------
    // Google Sign-In (WebAuthenticator / PKCE)
    // ------------------------------------------------------------------

    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            Uri authUrl = new(
                $"https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(GoogleClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(GoogleRedirectUri)}" +
                $"&response_type=token" +
                $"&scope=openid%20email%20profile");

            Uri callbackUri = new(GoogleRedirectUri);
            WebAuthenticatorResult result =
                await WebAuthenticator.AuthenticateAsync(authUrl, callbackUri);

            string? accessToken = result.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return false;

            // Fetch basic profile from Google userinfo endpoint.
            (string displayName, string email) = await FetchGoogleUserInfoAsync(accessToken);

            await PersistAuthAsync(displayName, email, "google", accessToken);
            return true;
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

    private static async Task<(string DisplayName, string Email)> FetchGoogleUserInfoAsync(
        string accessToken)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await client.GetAsync(
            "https://www.googleapis.com/oauth2/v3/userinfo");

        if (!response.IsSuccessStatusCode)
            return (string.Empty, string.Empty);

        string json = await response.Content.ReadAsStringAsync();
        // Simple extraction without a JSON library dependency.
        string name = ExtractJsonString(json, "name") ?? string.Empty;
        string email = ExtractJsonString(json, "email") ?? string.Empty;
        return (name, email);
    }

    // ------------------------------------------------------------------
    // Apple Sign-In
    // ------------------------------------------------------------------

    public async Task<bool> SignInWithAppleAsync()
    {
        try
        {
#if IOS || MACCATALYST
            WebAuthenticatorResult result =
                await AppleSignInAuthenticator.AuthenticateAsync(
                    new AppleSignInAuthenticator.Options
                    {
                        IncludeEmailScope = true,
                        IncludeFullNameScope = true
                    });

            string? email = result.Properties.GetValueOrDefault("email");
            string? firstName = result.Properties.GetValueOrDefault("firstName");
            string? lastName = result.Properties.GetValueOrDefault("lastName");
            string displayName = string.Join(" ", new[] { firstName, lastName }
                .Where(s => !string.IsNullOrEmpty(s)));

            await PersistAuthAsync(displayName, email ?? string.Empty, "apple", result.IdToken ?? string.Empty);
            return true;
#else
            // Apple Sign-In is only available on Apple platforms.
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

    // ------------------------------------------------------------------
    // Local-only mode
    // ------------------------------------------------------------------

    public void UseLocalOnly()
    {
        IsLocalOnly = true;
        IsAuthenticated = false;
        UserDisplayName = null;
        UserEmail = null;

        Preferences.Set(PrefsKeyIsLocalOnly, true);
        Preferences.Set(PrefsKeyIsAuthenticated, false);
        Preferences.Remove(PrefsKeyDisplayName);
        Preferences.Remove(PrefsKeyEmail);
        Preferences.Remove(PrefsKeyProvider);
        SecureStorage.Remove(SecureKeyAccessToken);
    }

    // ------------------------------------------------------------------
    // Sign out
    // ------------------------------------------------------------------

    public async Task SignOutAsync()
    {
        if (Preferences.Get(PrefsKeyProvider, "") == "microsoft")
        {
            IPublicClientApplication app = GetMsalClient();
            foreach (IAccount account in await app.GetAccountsAsync())
            {
                await app.RemoveAsync(account);
            }
        }

        ClearAuth();
    }

    // ------------------------------------------------------------------
    // Token access
    // ------------------------------------------------------------------

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!IsAuthenticated)
            return null;

        string? provider = Preferences.Get(PrefsKeyProvider, null as string);
        if (provider == "microsoft")
        {
            // Try silent refresh first to ensure the token is fresh.
            await TrySilentMicrosoftSignInAsync();
        }

        return await SecureStorage.GetAsync(SecureKeyAccessToken);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private IPublicClientApplication GetMsalClient()
    {
        if (_msalClient != null)
            return _msalClient;

        PublicClientApplicationBuilder builder = PublicClientApplicationBuilder
            .Create(MicrosoftClientId)
            .WithTenantId(MicrosoftTenantId);

#if ANDROID
        builder = builder
            .WithRedirectUri($"msauth://com.edalatpour.ben/{MicrosoftAndroidSignatureHash}")
            .WithParentActivityOrWindow(() => Platform.CurrentActivity);
#elif IOS || MACCATALYST
        builder = builder
            .WithRedirectUri("msauth.com.edalatpour.ben://auth")
            .WithParentActivityOrWindow(() =>
                UIKit.UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIKit.UIWindowScene>()
                    .FirstOrDefault()
                    ?.Windows
                    .FirstOrDefault(w => w.IsKeyWindow)
                    ?.RootViewController);
#elif WINDOWS
        builder = builder
            .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient");
#endif

        _msalClient = builder.Build();
        return _msalClient;
    }

    private async Task PersistAuthAsync(
        string displayName, string email, string provider, string accessToken)
    {
        IsAuthenticated = true;
        IsLocalOnly = false;
        UserDisplayName = displayName;
        UserEmail = email;

        Preferences.Set(PrefsKeyIsAuthenticated, true);
        Preferences.Set(PrefsKeyIsLocalOnly, false);
        Preferences.Set(PrefsKeyDisplayName, displayName);
        Preferences.Set(PrefsKeyEmail, email);
        Preferences.Set(PrefsKeyProvider, provider);
        await SecureStorage.SetAsync(SecureKeyAccessToken, accessToken);
    }

    private void ClearAuth()
    {
        IsAuthenticated = false;
        IsLocalOnly = false;
        UserDisplayName = null;
        UserEmail = null;

        Preferences.Set(PrefsKeyIsAuthenticated, false);
        Preferences.Set(PrefsKeyIsLocalOnly, false);
        Preferences.Remove(PrefsKeyDisplayName);
        Preferences.Remove(PrefsKeyEmail);
        Preferences.Remove(PrefsKeyProvider);
        SecureStorage.Remove(SecureKeyAccessToken);
    }

    private static string? ExtractJsonString(string json, string key)
    {
        string search = $"\"{key}\"";
        int keyIndex = json.IndexOf(search, StringComparison.Ordinal);
        if (keyIndex < 0)
            return null;

        int colonIndex = json.IndexOf(':', keyIndex + search.Length);
        if (colonIndex < 0)
            return null;

        int quoteStart = json.IndexOf('"', colonIndex + 1);
        if (quoteStart < 0)
            return null;

        int quoteEnd = json.IndexOf('"', quoteStart + 1);
        if (quoteEnd < 0)
            return null;

        return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }
}
