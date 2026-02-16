# Authentication Setup Guide

This guide explains how to configure authentication for the Ben Datasync Server and Client applications.

## Overview

The Ben application now implements **personal tables** (user-scoped data) where each user can only access their own Notes and Tasks. This requires authentication via Microsoft, Google, or Entra ID accounts.

## Server-Side Configuration (Ben.Datasync.Server)

### 1. Authentication Packages

The following packages have been added to support authentication:
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT Bearer token authentication
- `Microsoft.Identity.Web` - Microsoft Identity platform integration

### 2. Database Schema Changes

Both `NoteItem` and `TaskItem` now include a `UserId` field:
- `UserId` (string, required) - Identifies the owner of the record

### 3. Access Control

The `PersonalAccessControlProvider<T>` ensures that:
- Users can only query their own data
- Users can only create/update/delete their own records
- The `UserId` is automatically set to the authenticated user's ID

### 4. Configuration

Update `appsettings.json` with your Azure AD/Entra ID configuration:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "yourdomain.onmicrosoft.com",
    "TenantId": "common",
    "ClientId": "your-client-id-here"
  }
}
```

**Configuration Values:**
- `Instance`: The Azure AD authentication endpoint (usually `https://login.microsoftonline.com/`)
- `Domain`: Your Azure AD domain (e.g., `contoso.onmicrosoft.com`)
- `TenantId`: 
  - Use `common` to allow any Microsoft account (personal or work/school)
  - Use `organizations` to allow only work/school accounts
  - Use `consumers` to allow only personal Microsoft accounts
  - Use your specific tenant ID to restrict to your organization
- `ClientId`: The Application (client) ID from your Azure AD app registration

### 5. Setting up Azure AD App Registration

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure:
   - **Name**: Ben Datasync Server API
   - **Supported account types**: Choose based on your needs
   - **Redirect URI**: Not needed for API
5. After creation, note the **Application (client) ID**
6. Go to **Expose an API**
   - Add a scope (e.g., `api://your-client-id/access_as_user`)
7. Update your `appsettings.json` with the Client ID

## Client-Side Configuration (Ben.Client)

### Required Changes

To enable authentication in the MAUI client, you need to:

### 1. Add Authentication Packages

Add the following NuGet packages to `Ben.Client.csproj`:

```xml
<PackageReference Include="Microsoft.Identity.Client" Version="4.65.0" />
<PackageReference Include="Microsoft.Identity.Client.Extensions.Msal" Version="4.65.0" />
```

### 2. Configure Authentication in MauiProgram.cs

Add authentication configuration to your `MauiProgram.cs`:

```csharp
using Microsoft.Identity.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // ... existing configuration ...

        // Configure MSAL (Microsoft Authentication Library)
        var pca = PublicClientApplicationBuilder
            .Create("your-client-id-here") // Use the same or different client ID
            .WithAuthority(AzureCloudInstance.AzurePublic, "common")
            .WithRedirectUri("msal{your-client-id}://auth") // Must match Azure AD config
            .Build();

        builder.Services.AddSingleton<IPublicClientApplication>(pca);
        
        // ... rest of configuration ...
    }
}
```

### 3. Create an Authentication Service

Create a new file `Services/AuthenticationService.cs`:

```csharp
using Microsoft.Identity.Client;

namespace Ben.Services;

public class AuthenticationService
{
    private readonly IPublicClientApplication _pca;
    private string? _accessToken;

    public AuthenticationService(IPublicClientApplication pca)
    {
        _pca = pca;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken))
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
                // Try to get token silently
                result = await _pca.AcquireTokenSilent(
                    new[] { "api://your-server-client-id/access_as_user" },
                    account)
                    .ExecuteAsync();
            }
            else
            {
                // Interactive login
                result = await _pca.AcquireTokenInteractive(
                    new[] { "api://your-server-client-id/access_as_user" })
                    .ExecuteAsync();
            }

            _accessToken = result.AccessToken;
            return _accessToken;
        }
        catch (MsalException ex)
        {
            // Handle authentication errors
            Console.WriteLine($"Authentication failed: {ex.Message}");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        var accounts = await _pca.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _pca.RemoveAsync(account);
        }
        _accessToken = null;
    }
}
```

### 4. Update DatasyncOptions to Include Authentication

Modify `MauiProgram.cs` to configure the Datasync client with authentication:

```csharp
builder.Services.AddSingleton<AuthenticationService>();

builder.Services.AddSingleton(sp =>
{
    var authService = sp.GetRequiredService<AuthenticationService>();
    
    return new DatasyncOptions
    {
        Endpoint = new Uri("https://app-qg762nqxq5bva.azurewebsites.net/"),
        HttpClientOptions = new DatasyncHttpClientOptions
        {
            HttpMessageHandlers = new[]
            {
                new DelegatingHandler[]
                {
                    new AuthenticationHandler(authService)
                }
            }
        }
    };
});
```

### 5. Create an Authentication Handler

Create a new file `Services/AuthenticationHandler.cs`:

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

### 6. Azure AD App Registration for Client

1. In the same or different app registration in Azure AD
2. Go to **Authentication** > **Add a platform** > **Mobile and desktop applications**
3. Add redirect URI: `msal{your-client-id}://auth`
4. Under **API permissions**, add:
   - Microsoft Graph (delegated): `User.Read`
   - Your server API: `access_as_user` (the scope you created earlier)
5. Grant admin consent if required

### 7. Platform-Specific Configuration

#### Android
Add to `AndroidManifest.xml`:
```xml
<activity android:name="microsoft.identity.client.BrowserTabActivity">
    <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="msal{your-client-id}" android:host="auth" />
    </intent-filter>
</activity>
```

#### iOS
Add to `Info.plist`:
```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>msal{your-client-id}</string>
        </array>
    </dict>
</array>
```

### 8. Prompt User to Login

In your `App.xaml.cs` or startup code, prompt the user to login:

```csharp
protected override async void OnStart()
{
    base.OnStart();
    
    var authService = Handler.MauiContext.Services.GetRequiredService<AuthenticationService>();
    var token = await authService.GetAccessTokenAsync();
    
    if (string.IsNullOrEmpty(token))
    {
        // Show login required message
        await Shell.Current.DisplayAlert(
            "Login Required", 
            "Please login to continue", 
            "OK");
    }
}
```

## Supporting Google Authentication

To support Google authentication alongside Microsoft:

1. The server is already configured to accept JWT tokens from Azure AD with `TenantId: "common"`
2. For Google, you would need to add Google authentication:

```csharp
// In Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(...)
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"];
        options.ClientSecret = builder.Configuration["Google:ClientSecret"];
    });
```

3. In the client, use Google Sign-In libraries for your platform

## Migration Notes

When deploying this update:

1. **Database Migration**: The `UserId` column will be added to both tables
2. **Existing Data**: Existing records without a `UserId` will need to be:
   - Deleted, or
   - Assigned to a default user, or
   - Migrated using a script

3. **Client Updates**: All clients must be updated to support authentication before the server enforces authorization

## Testing

1. **Without Authentication**: Requests will return empty result sets (401 Unauthorized)
2. **With Authentication**: Users will only see their own data
3. **Test User Isolation**: Create items as different users and verify they cannot see each other's data

## Troubleshooting

### Common Issues

1. **401 Unauthorized**: Token is missing or invalid
   - Verify token is being sent in Authorization header
   - Check token expiration
   - Verify audience and issuer claims

2. **Empty Result Set**: User ID not found in token claims
   - Check which claim type contains the user ID
   - Verify PersonalAccessControlProvider is checking the right claims

3. **Cross-User Data Access**: Access control not working
   - Verify controllers have `[Authorize]` attribute
   - Check that AccessControlProvider is registered in DI container
   - Ensure UserId is being set on entity creation

## Security Considerations

1. **Never trust client-side filtering**: All authorization is enforced server-side
2. **Use HTTPS**: Always use HTTPS in production
3. **Token Storage**: Store tokens securely using platform-specific secure storage
4. **Token Expiration**: Implement token refresh logic
5. **Logout**: Properly clear tokens on logout
