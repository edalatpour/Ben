# Multi-Provider Authentication Guide

This guide explains how the Ben Datasync Server supports authentication from multiple identity providers, including Personal Microsoft accounts and Google accounts.

## Overview

The server is configured to accept JWT tokens from multiple identity providers:
- **Microsoft Personal Accounts** (e.g., outlook.com, hotmail.com)
- **Microsoft Work/School Accounts** (Azure AD organizational accounts)
- **Google Accounts** (e.g., gmail.com, Google Workspace)

This is achieved through a flexible JWT Bearer authentication configuration that validates tokens from multiple issuers.

## How It Works

### Token Validation

The server validates JWT tokens by checking:

1. **Issuer**: The token must be issued by a trusted identity provider
   - Microsoft: `https://login.microsoftonline.com/{tenantId}/v2.0`
   - Google: `https://accounts.google.com`

2. **Audience**: The token must be intended for this API
   - Your Microsoft Client ID (from Azure AD app registration)
   - Your Google Client ID (from Google Cloud Console)

3. **Signature**: The token must be signed by the identity provider's signing key

4. **Expiration**: The token must not be expired

### User Identification

The `PersonalAccessControlProvider` extracts the user ID from JWT claims in this order:

1. **`sub`** - Standard OpenID Connect subject claim (used by both Microsoft and Google)
2. **`NameIdentifier`** - Standard .NET claim type
3. **`oid`** - Microsoft Azure AD object ID
4. **`user_id`** - Additional fallback for some providers

This ensures consistent user identification regardless of which provider issued the token.

## Configuration

### Server Configuration (appsettings.json)

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "common",
    "ClientId": "your-microsoft-client-id-here"
  },
  "GoogleAuth": {
    "ClientId": "your-google-client-id-here.apps.googleusercontent.com"
  }
}
```

**AzureAd Configuration:**
- `Instance`: Microsoft login endpoint
- `TenantId`: Set to `"common"` to accept both personal and work/school accounts
- `ClientId`: Your Azure AD app registration client ID

**GoogleAuth Configuration:**
- `ClientId`: Your Google OAuth 2.0 client ID

### Setting Up Microsoft Authentication

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Configure:
   - **Name**: Ben Datasync API
   - **Supported account types**: Select **"Accounts in any organizational directory and personal Microsoft accounts"**
   - This is crucial for supporting both personal and work accounts
5. After creation, note the **Application (client) ID**
6. Go to **Expose an API** > **Add a scope**
   - Scope name: `access_as_user`
   - Who can consent: **Admins and users**
7. Update `appsettings.json` with your Client ID

### Setting Up Google Authentication

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create a new project or select existing one
3. Enable **Google+ API** or **Google Identity** services
4. Go to **Credentials**
5. Click **Create Credentials** > **OAuth 2.0 Client ID**
6. Configure:
   - **Application type**: Web application (for the API backend) or appropriate type for your client
   - **Authorized redirect URIs**: Your client app redirect URIs
7. Note the **Client ID** (format: `xxx.apps.googleusercontent.com`)
8. Update `appsettings.json` with your Google Client ID

**Important**: The Google Client ID configured on the server must match the client ID your mobile/web clients use to obtain tokens.

## Client Implementation

### Microsoft Authentication (Personal Accounts)

Use Microsoft Authentication Library (MSAL) with these settings:

```csharp
var pca = PublicClientApplicationBuilder
    .Create("your-microsoft-client-id")
    .WithAuthority(AzureCloudInstance.AzurePublic, "common") // "common" for personal accounts
    .WithRedirectUri("msal{your-client-id}://auth")
    .Build();

// Acquire token
var result = await pca.AcquireTokenInteractive(
    new[] { "api://your-server-api-client-id/access_as_user" })
    .ExecuteAsync();

string accessToken = result.AccessToken;
```

**Key Points:**
- Use `"common"` tenant to support personal Microsoft accounts
- Request scope format: `api://{server-client-id}/access_as_user`
- The access token will have `sub` claim with user's unique ID

### Google Authentication

Use Google Sign-In SDK for your platform:

#### Android
```kotlin
val gso = GoogleSignInOptions.Builder(GoogleSignInOptions.DEFAULT_SIGN_IN)
    .requestIdToken("your-google-client-id.apps.googleusercontent.com")
    .requestEmail()
    .build()

val googleSignInClient = GoogleSignIn.getClient(this, gso)

// After sign-in
val account = GoogleSignIn.getSignedInAccountFromIntent(data).result
val idToken = account.idToken // This is your JWT token
```

#### iOS
```swift
GIDSignIn.sharedInstance.signIn(with: config, presenting: self) { user, error in
    guard let user = user else { return }
    let idToken = user.authentication.idToken // This is your JWT token
}
```

#### MAUI/.NET
```csharp
// Use a library like GoogleSignInPlatform or implement web-based OAuth flow
// The token obtained should be a JWT ID token
```

**Key Points:**
- Request `idToken` (not just access token)
- The ID token is a JWT that can be validated by your server
- The token will have `sub` claim with user's Google unique ID
- Token audience will be your Google Client ID

### Sending Tokens to Server

Regardless of provider, include the token in the Authorization header:

```csharp
public class AuthenticationHandler : DelegatingHandler
{
    private readonly IAuthenticationService _authService;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Get token from either Microsoft or Google authentication
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

## User Identity Consistency

### Challenge: Different User IDs

Each identity provider uses different user ID formats:
- **Microsoft**: GUID format (e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
- **Google**: Numeric string (e.g., `123456789012345678901`)

### Solution: Provider-Specific User IDs

Users are identified by their provider-specific user ID. This means:
- A user signing in with Microsoft gets a Microsoft-based user ID
- A user signing in with Google gets a Google-based user ID
- **These are treated as different users** even if they use the same email address

**Implications:**
- Data is not shared between different authentication methods for the same person
- If a user signs in with Microsoft one day and Google the next, they'll see different data
- This is by design for security and data isolation

### Alternative: Email-Based User Identity

If you want to merge users across providers (same email = same user), you would need to:

1. Extract email from claims instead of user ID
2. Modify `PersonalAccessControlProvider.GetUserIdFromClaims()`:

```csharp
private string? GetUserIdFromClaims(ClaimsPrincipal? user)
{
    if (user?.Identity?.IsAuthenticated != true)
    {
        return null;
    }

    // Use email as universal identifier
    return user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.FindFirst("email")?.Value
        ?? user.FindFirst("preferred_username")?.Value;
}
```

3. Update `UserId` column in database to store emails instead
4. Consider email verification requirements

**Security Consideration**: Email-based identity requires trusting that both providers verify email addresses, which they do.

## Testing

### Testing with Microsoft Personal Account

1. **Obtain Token**:
   - Use MSAL to sign in with a personal Microsoft account (outlook.com, hotmail.com)
   - Request scope: `api://your-server-api-client-id/access_as_user`

2. **Decode Token**:
   - Go to https://jwt.ms and paste the token
   - Verify:
     - `iss`: `https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0`  
       *(Note: 9188040d-6c67-4c5b-b112-36a304b66dad is the Microsoft-assigned tenant ID that ALL personal accounts share - this is not a placeholder)*
     - `aud`: Your Microsoft client ID
     - `sub`: User's unique ID

3. **Test API Call**:
   ```bash
   curl -H "Authorization: Bearer YOUR_TOKEN" \
        https://your-api-url/tables/noteitem
   ```

### Testing with Google Account

1. **Obtain Token**:
   - Use Google Sign-In SDK to sign in
   - Get the ID token (not access token)

2. **Decode Token**:
   - Go to https://jwt.ms and paste the token
   - Verify:
     - `iss`: `https://accounts.google.com` or `accounts.google.com`
     - `aud`: Your Google client ID
     - `sub`: User's Google unique ID

3. **Test API Call**:
   ```bash
   curl -H "Authorization: Bearer YOUR_TOKEN" \
        https://your-api-url/tables/noteitem
   ```

### Expected Results

- **With valid Microsoft token**: 200 OK, returns user's data
- **With valid Google token**: 200 OK, returns user's data (different data than Microsoft)
- **Without token**: 401 Unauthorized
- **With invalid token**: 401 Unauthorized
- **With expired token**: 401 Unauthorized

## Troubleshooting

### "The issuer is invalid"

**Problem**: Token issuer is not in the list of valid issuers.

**Solution**: 
1. Decode your token at https://jwt.ms
2. Check the `iss` claim value
3. Add it to the `ValidIssuers` array in Program.cs
4. Common issuers:
   - Microsoft personal: `https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0`
   - Microsoft org: `https://login.microsoftonline.com/{tenant-id}/v2.0`
   - Google: `https://accounts.google.com` or `accounts.google.com`

### "The audience is invalid"

**Problem**: Token audience doesn't match configured client IDs.

**Solution**:
1. Decode your token at https://jwt.ms
2. Check the `aud` claim value
3. Ensure it matches:
   - For Microsoft: Your Azure AD app client ID
   - For Google: Your Google OAuth client ID
4. Update `ValidAudiences` in Program.cs if needed

### "User ID not found"

**Problem**: PersonalAccessControlProvider can't extract user ID from token.

**Solution**:
1. Decode your token at https://jwt.ms
2. Check which claim contains the user ID
3. Common claims: `sub`, `oid`, `user_id`
4. Verify `GetUserIdFromClaims()` checks for that claim type
5. Add custom claim type if needed

### "Cross-provider data access"

**Problem**: User signed in with Google but can't see data created with Microsoft.

**Solution**: This is expected behavior. Each provider gives a different user ID. Options:
1. Accept separate identities (recommended for security)
2. Implement email-based identity (see "Email-Based User Identity" section)
3. Implement account linking in your app

### Google Token: "Invalid signature"

**Problem**: Google token signature validation fails.

**Solution**:
1. Ensure you're using an **ID token**, not an access token
2. Google ID tokens are self-contained JWTs that can be validated
3. Server needs to fetch Google's public keys from: `https://www.googleapis.com/oauth2/v3/certs`
4. This is automatically handled by JWT Bearer authentication middleware

## Security Considerations

### Token Validation

- ✅ **Issuer validation**: Only accepts tokens from trusted providers
- ✅ **Audience validation**: Only accepts tokens meant for this API
- ✅ **Signature validation**: Verifies token hasn't been tampered with
- ✅ **Expiration validation**: Rejects expired tokens
- ✅ **HTTPS only**: Always use HTTPS in production

### User Identity

- ✅ **Provider-specific**: Users are isolated by authentication provider
- ✅ **Claim-based**: User ID extracted from verified JWT claims
- ✅ **Server-enforced**: Clients cannot spoof user ID
- ✅ **Data isolation**: PersonalAccessControlProvider enforces data view filters

### Best Practices

1. **Use HTTPS**: Always use HTTPS in production to prevent token interception
2. **Validate tokens server-side**: Never trust client-side validation
3. **Keep signing keys updated**: JWT middleware automatically refreshes signing keys
4. **Monitor token expiration**: Implement token refresh in clients
5. **Log authentication failures**: Help debug and detect attacks
6. **Rate limiting**: Implement rate limiting to prevent abuse

## Advanced Configuration

### Adding More Providers

To add support for additional identity providers (Facebook, Twitter, Auth0, etc.):

1. Add the provider's issuer to `ValidIssuers`:
```csharp
ValidIssuers = new[]
{
    // ... existing issuers ...
    "https://facebook.com",
    "https://auth0-tenant.auth0.com/"
}
```

2. Add the provider's client ID to `ValidAudiences`:
```csharp
ValidAudiences = new[]
{
    // ... existing audiences ...
    builder.Configuration["FacebookAuth:ClientId"] ?? "",
    builder.Configuration["Auth0:ClientId"] ?? ""
}
```

3. Ensure the provider issues standard JWT tokens with `sub` claim

### Custom User ID Extraction

If a provider uses non-standard claim names:

```csharp
private string? GetUserIdFromClaims(ClaimsPrincipal? user)
{
    // ... existing checks ...
    
    // Add custom provider check
    ?? user.FindFirst("custom_user_id")?.Value
    ?? user.FindFirst("uid")?.Value;
}
```

### Multiple Audiences per Provider

If you have multiple client applications:

```csharp
ValidAudiences = new[]
{
    // Web app
    builder.Configuration["AzureAd:WebClientId"] ?? "",
    // Mobile app
    builder.Configuration["AzureAd:MobileClientId"] ?? "",
    // Google web
    builder.Configuration["GoogleAuth:WebClientId"] ?? "",
    // Google mobile
    builder.Configuration["GoogleAuth:MobileClientId"] ?? ""
}
```

## Summary

The Ben Datasync Server now supports:
- ✅ Microsoft Personal Accounts (outlook.com, hotmail.com, live.com)
- ✅ Microsoft Work/School Accounts (Azure AD)
- ✅ Google Accounts (gmail.com, Google Workspace)
- ✅ Extensible to additional providers

Each provider maintains separate user identities for security. The implementation uses standard JWT Bearer authentication with multi-issuer support, making it compatible with any OpenID Connect compliant provider.

For client implementation details, see:
- `CLIENT_AUTH_GUIDE.md` - MAUI client setup
- `AUTHENTICATION_SETUP.md` - General authentication guide
- `IMPLEMENTATION_SUMMARY.md` - Technical overview
