# Client-Side Authentication Implementation Guide

This document provides step-by-step instructions for implementing authentication in the Ben MAUI client application to work with the newly secured Datasync Server.

## Overview

The server now requires authentication for all Notes and Tasks operations. You must implement authentication in the client to:
1. Allow users to sign in with Microsoft, Google, or Entra ID
2. Obtain an access token
3. Include that token in all Datasync requests

## Prerequisites

Before you begin, ensure you have:
- An Azure AD app registration configured (see AUTHENTICATION_SETUP.md for details)
- The Application (client) ID from your Azure AD app
- The API scope exposed by your server app (e.g., `api://your-server-client-id/access_as_user`)

## Implementation Steps

### Step 1: Add Required NuGet Packages

Add these packages to `Ben.Client/Ben.Client.csproj`:

```xml
<PackageReference Include="Microsoft.Identity.Client" Version="4.65.0" />
<PackageReference Include="Microsoft.Identity.Client.Extensions.Msal" Version="4.65.0" />
```

Then run: `dotnet restore`

### Step 2: Create Authentication Service

Create a new file: `Ben.Client/Services/AuthenticationService.cs`

```csharp
using Microsoft.Identity.Client;

namespace Ben.Services;

public class AuthenticationService
{
    private readonly IPublicClientApplication _pca;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiration = DateTimeOffset.MinValue;

    // Replace with your actual client ID and scopes
    private const string ClientId = "YOUR-CLIENT-ID-HERE";
    private readonly string[] _scopes = new[] { "api://YOUR-SERVER-CLIENT-ID/access_as_user" };

    public AuthenticationService(IPublicClientApplication pca)
    {
        _pca = pca;
    }

    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiration)
        {
            return _accessToken;
        }

        try
        {
            var accounts = await _pca.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            AuthenticationResult result;

            if (account != null)
            {
                // Try to get token silently (from cache or refresh token)
                try
                {
                    result = await _pca.AcquireTokenSilent(_scopes, account)
                        .ExecuteAsync();
                }
                catch (MsalUiRequiredException)
                {
                    // Silent acquisition failed, need interactive login
                    result = await AcquireTokenInteractiveAsync();
                }
            }
            else
            {
                // No account found, need interactive login
                result = await AcquireTokenInteractiveAsync();
            }

            _accessToken = result.AccessToken;
            _tokenExpiration = result.ExpiresOn;
            return _accessToken;
        }
        catch (MsalException ex)
        {
            // Handle authentication errors
            System.Diagnostics.Debug.WriteLine($"Authentication failed: {ex.Message}");
            return null;
        }
    }

    private async Task<AuthenticationResult> AcquireTokenInteractiveAsync()
    {
        return await _pca.AcquireTokenInteractive(_scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync();
    }

    public async Task LogoutAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _pca.RemoveAsync(account);
        }
        _accessToken = null;
        _tokenExpiration = DateTimeOffset.MinValue;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        return accounts.Any();
    }

    public async Task<string?> GetUserDisplayNameAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        return accounts.FirstOrDefault()?.Username;
    }
}
```

### Step 3: Create HTTP Message Handler for Authentication

Create: `Ben.Client/Services/AuthenticationHandler.cs`

```csharp
using System.Net.Http.Headers;

namespace Ben.Services;

public class AuthenticationHandler : DelegatingHandler
{
    private readonly AuthenticationService _authService;

    public AuthenticationHandler(AuthenticationService authService)
    {
        _authService = authService;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync();
        
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### Step 4: Update MauiProgram.cs

Modify `Ben.Client/MauiProgram.cs` to configure authentication and the Datasync client:

```csharp
using Microsoft.Identity.Client;
using Ben.Services;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // ... existing configuration ...

        // Configure MSAL (Microsoft Authentication Library)
        var pca = PublicClientApplicationBuilder
            .Create("YOUR-CLIENT-ID-HERE") // Replace with your Azure AD client ID
            .WithAuthority(AzureCloudInstance.AzurePublic, "common") // "common" for multi-tenant
            .WithRedirectUri("msal{YOUR-CLIENT-ID-HERE}://auth") // Must match Azure AD config
            .WithIosKeychainSecurityGroup("com.microsoft.adalcache")
            .Build();

        builder.Services.AddSingleton<IPublicClientApplication>(pca);
        builder.Services.AddSingleton<AuthenticationService>();

        // Update DatasyncOptions to include authentication
        builder.Services.AddSingleton(sp =>
        {
            var authService = sp.GetRequiredService<AuthenticationService>();
            var httpClient = new HttpClient(new AuthenticationHandler(authService));

            return new DatasyncOptions
            {
                Endpoint = new Uri("https://app-qg762nqxq5bva.azurewebsites.net/"),
                HttpClient = httpClient
            };
        });

        // ... rest of configuration ...
    }
}
```

### Step 5: Platform-Specific Configuration

#### Android

Add to `Platforms/Android/AndroidManifest.xml` inside the `<application>` tag:

```xml
<activity android:name="microsoft.identity.client.BrowserTabActivity" android:exported="true">
    <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data 
            android:scheme="msal{YOUR-CLIENT-ID-HERE}" 
            android:host="auth" />
    </intent-filter>
</activity>
```

Also add to `Platforms/Android/MainActivity.cs`:

```csharp
using Microsoft.Identity.Client;

protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
{
    base.OnActivityResult(requestCode, resultCode, data);
    AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data);
}
```

#### iOS / MacCatalyst

Add to `Platforms/iOS/Info.plist`:

```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>msal{YOUR-CLIENT-ID-HERE}</string>
        </array>
    </dict>
</array>
<key>LSApplicationQueriesSchemes</key>
<array>
    <string>msauthv2</string>
    <string>msauthv3</string>
</array>
```

Add to `Platforms/iOS/AppDelegate.cs`:

```csharp
using Microsoft.Identity.Client;

public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
{
    if (AuthenticationContinuationHelper.IsBrokerResponse(null))
    {
        AuthenticationContinuationHelper.SetBrokerContinuationEventArgs(url);
        return true;
    }
    
    return base.OpenUrl(application, url, options);
}
```

#### Windows

No additional platform-specific configuration needed for Windows.

### Step 6: Add Login UI

Create a login page or modify your startup flow to prompt for login. Example in `App.xaml.cs`:

```csharp
protected override async void OnStart()
{
    base.OnStart();
    
    var authService = Handler?.MauiContext?.Services.GetRequiredService<AuthenticationService>();
    
    if (authService != null)
    {
        bool isAuthenticated = await authService.IsAuthenticatedAsync();
        
        if (!isAuthenticated)
        {
            // Prompt user to login
            bool loginResult = await Shell.Current.DisplayAlert(
                "Login Required", 
                "Please login to access your notes and tasks", 
                "Login", 
                "Cancel");
            
            if (loginResult)
            {
                var token = await authService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(token))
                {
                    await Shell.Current.DisplayAlert(
                        "Login Failed", 
                        "Could not authenticate. Please try again.", 
                        "OK");
                }
                else
                {
                    var userName = await authService.GetUserDisplayNameAsync();
                    await Shell.Current.DisplayAlert(
                        "Login Successful", 
                        $"Welcome, {userName}!", 
                        "OK");
                }
            }
        }
    }
}
```

### Step 7: Add Logout Functionality

Add a logout button to your UI and call:

```csharp
private async Task OnLogoutClicked()
{
    var authService = Handler.MauiContext.Services.GetRequiredService<AuthenticationService>();
    await authService.LogoutAsync();
    
    await Shell.Current.DisplayAlert("Logged Out", "You have been logged out successfully.", "OK");
    
    // Optionally navigate to login page or close app
}
```

## Testing

1. **Clean Build**: Delete `bin` and `obj` folders, then rebuild
2. **First Launch**: App should prompt for login
3. **Sign In**: Choose Microsoft account (personal or work/school)
4. **Verify**: After sign-in, app should sync data with server
5. **Subsequent Launches**: Token should be cached, no re-login needed
6. **Token Expiration**: When token expires (typically 1 hour), silent refresh should occur

## Troubleshooting

### Common Issues

1. **"Invalid redirect URI"**
   - Ensure redirect URI in Azure AD matches exactly: `msal{YOUR-CLIENT-ID}://auth`
   - Check that platform-specific configuration is correct

2. **"Cannot obtain token"**
   - Verify client ID is correct
   - Check API permissions are configured in Azure AD
   - Ensure API scope string is correct

3. **"401 Unauthorized" from server**
   - Token may not be included in requests - check AuthenticationHandler
   - Token may be expired - ensure token refresh logic works
   - API scope may be incorrect

4. **Android: "No Activity found to handle Intent"**
   - Verify AndroidManifest.xml has correct BrowserTabActivity configuration
   - Check scheme matches your client ID

5. **iOS: "Cannot open URL"**
   - Verify Info.plist has correct URL scheme
   - Check CFBundleURLSchemes matches your client ID

### Debug Tips

1. **Enable MSAL Logging**:
   ```csharp
   var pca = PublicClientApplicationBuilder
       .Create("...")
       .WithLogging((level, message, pii) =>
       {
           System.Diagnostics.Debug.WriteLine($"MSAL {level}: {message}");
       }, LogLevel.Verbose, enablePiiLogging: true)
       .Build();
   ```

2. **Check Token Claims**:
   After successful auth, decode the JWT token at https://jwt.ms to verify claims

3. **Network Inspection**:
   Use tools like Fiddler to inspect HTTP requests and verify Bearer token is included

## Azure AD App Registration Setup

If you haven't set up Azure AD yet:

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Name: "Ben Client App"
5. Supported account types: **Accounts in any organizational directory and personal Microsoft accounts**
6. Redirect URI: Select **Public client/native** and add `msal{YOUR-CLIENT-ID}://auth`
7. Click **Register**
8. Note the **Application (client) ID**
9. Go to **Authentication** > **Platform configurations** > **Add a platform**
10. Select **Mobile and desktop applications**
11. Add redirect URI: `msal{YOUR-CLIENT-ID}://auth`
12. Go to **API permissions**
13. Click **Add a permission** > **My APIs**
14. Select your server API app
15. Check **access_as_user** permission
16. Click **Grant admin consent** (if you have admin privileges)

## Important Configuration Values to Replace

Before deploying, replace these placeholder values in your code:

- `YOUR-CLIENT-ID-HERE` - Your Azure AD app registration client ID
- `YOUR-SERVER-CLIENT-ID` - Your server API app registration client ID
- API scope: `api://YOUR-SERVER-CLIENT-ID/access_as_user`
- Redirect URI: `msal{YOUR-CLIENT-ID-HERE}://auth`

## Security Best Practices

1. **Never commit client secrets** - Use user secrets or environment variables
2. **Use HTTPS** - Always use secure connections in production
3. **Token Storage** - MSAL handles secure token storage automatically
4. **Handle Errors Gracefully** - Always handle authentication failures
5. **Refresh Tokens** - MSAL handles token refresh automatically
6. **Logout on Sensitive Operations** - Consider forcing re-auth for sensitive actions

## Next Steps

After implementing authentication:

1. Test the full authentication flow
2. Verify data isolation (users should only see their own data)
3. Test offline sync still works
4. Test token refresh on expiration
5. Add proper error handling and user feedback
6. Consider adding biometric authentication for re-auth

## Support

For issues:
- Check the Microsoft Identity documentation: https://docs.microsoft.com/en-us/azure/active-directory/develop/
- MSAL GitHub: https://github.com/AzureAD/microsoft-authentication-library-for-dotnet
- Datasync Toolkit: https://github.com/CommunityToolkit/Datasync
