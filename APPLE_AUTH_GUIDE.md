# Apple ID Authentication Guide

This guide explains how to add Apple ID authentication support to the Ben Datasync Server and Client applications.

## Overview

Apple's "Sign in with Apple" uses standard OpenID Connect, making it fully compatible with the existing multi-provider authentication infrastructure. The server already supports Apple ID authentication alongside Microsoft and Google accounts.

## How It Works

### Apple Sign in with Apple

- **Issuer**: `https://appleid.apple.com`
- **OpenID Configuration**: https://appleid.apple.com/.well-known/openid-configuration
- **User Identification**: Uses standard `sub` (subject) claim
- **Token Type**: JWT ID tokens
- **Validation**: Standard OpenID Connect token validation

Apple tokens are validated the same way as Microsoft and Google tokens:
1. Verify token signature using Apple's public keys
2. Verify issuer is `https://appleid.apple.com`
3. Verify audience matches your Apple Services ID
4. Verify token hasn't expired
5. Extract user ID from `sub` claim

## Server Configuration

The server is already configured to accept Apple ID tokens. You just need to add your Apple Services ID to the configuration.

### appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "common",
    "ClientId": "your-microsoft-client-id"
  },
  "GoogleAuth": {
    "ClientId": "your-google-client-id.apps.googleusercontent.com"
  },
  "AppleAuth": {
    "ClientId": "your.apple.services.id"
  }
}
```

**AppleAuth Configuration:**
- `ClientId`: Your Apple Services ID (not your App ID)
  - Format: Reverse domain notation (e.g., `com.yourcompany.yourapp`)
  - This is the Services ID you create in Apple Developer Console

## Setting Up Apple Sign in with Apple

### Prerequisites

- Apple Developer Account (paid membership required)
- Enrolled in Apple Developer Program
- App ID configured for your application

### Step 1: Create an App ID (if not already created)

1. Go to [Apple Developer Console](https://developer.apple.com/account/)
2. Navigate to **Certificates, Identifiers & Profiles**
3. Select **Identifiers** > **App IDs**
4. Click the **+** button or **Register an App ID**
5. Configure:
   - **Description**: Your app name
   - **Bundle ID**: Your app's bundle identifier (e.g., `com.yourcompany.ben`)
   - **Capabilities**: Check **Sign in with Apple**
6. Click **Continue** and **Register**

### Step 2: Create a Services ID

1. In Apple Developer Console, navigate to **Identifiers**
2. Click the **+** button
3. Select **Services IDs** and click **Continue**
4. Configure:
   - **Description**: Your service name (e.g., "Ben Datasync API")
   - **Identifier**: Reverse domain notation (e.g., `com.yourcompany.ben.api`)
   - This identifier is your **Services ID** (ClientId for the server)
5. Click **Continue** and **Register**

### Step 3: Configure Sign in with Apple for Services ID

1. Select your newly created Services ID
2. Check **Sign in with Apple**
3. Click **Configure**
4. Configure domains and redirect URLs:
   - **Domains and Subdomains**: Your API domain (e.g., `api.yourcompany.com`)
   - **Return URLs**: Your client app redirect URLs
     - iOS: `https://yourapp.com/callback` or deep link URL
     - Web: `https://yourwebapp.com/auth/callback`
5. Click **Save** and **Continue**
6. Click **Register**

### Step 4: Update Server Configuration

Update your `appsettings.json` with the Services ID:

```json
{
  "AppleAuth": {
    "ClientId": "com.yourcompany.ben.api"
  }
}
```

## Client Implementation

### iOS / MacCatalyst (Native)

Use Apple's AuthenticationServices framework:

```swift
import AuthenticationServices

class AppleSignInManager: NSObject, ASAuthorizationControllerDelegate {
    func signInWithApple() {
        let provider = ASAuthorizationAppleIDProvider()
        let request = provider.createRequest()
        request.requestedScopes = [.fullName, .email]
        
        let controller = ASAuthorizationController(authorizationRequests: [request])
        controller.delegate = self
        controller.presentationContextProvider = self
        controller.performRequests()
    }
    
    func authorizationController(controller: ASAuthorizationController, 
                                didCompleteWithAuthorization authorization: ASAuthorization) {
        if let appleIDCredential = authorization.credential as? ASAuthorizationAppleIDCredential {
            let userID = appleIDCredential.user
            let identityToken = appleIDCredential.identityToken
            
            if let tokenData = identityToken,
               let tokenString = String(data: tokenData, encoding: .utf8) {
                // This is your JWT ID token - send it to the API
                sendTokenToAPI(tokenString)
            }
        }
    }
}
```

### MAUI/.NET Implementation

For MAUI applications, you can use platform-specific implementations:

```csharp
// Interface
public interface IAppleSignInService
{
    Task<string?> SignInAsync();
}

// iOS Implementation (Platforms/iOS/AppleSignInService.cs)
using AuthenticationServices;
using Foundation;

public class AppleSignInService : IAppleSignInService
{
    public async Task<string?> SignInAsync()
    {
        var provider = new ASAuthorizationAppleIdProvider();
        var request = provider.CreateRequest();
        request.RequestedScopes = new[] { 
            ASAuthorizationScope.FullName, 
            ASAuthorizationScope.Email 
        };
        
        var controller = new ASAuthorizationController(new[] { request });
        
        var tcs = new TaskCompletionSource<string?>();
        
        controller.Delegate = new AppleSignInDelegate(tcs);
        controller.PerformRequests();
        
        return await tcs.Task;
    }
}

class AppleSignInDelegate : ASAuthorizationControllerDelegate
{
    private readonly TaskCompletionSource<string?> _tcs;
    
    public AppleSignInDelegate(TaskCompletionSource<string?> tcs)
    {
        _tcs = tcs;
    }
    
    public override void DidComplete(ASAuthorizationController controller, 
                                     ASAuthorization authorization)
    {
        if (authorization.Credential is ASAuthorizationAppleIdCredential credential)
        {
            var token = NSString.FromData(credential.IdentityToken, NSStringEncoding.UTF8);
            _tcs.SetResult(token?.ToString());
        }
        else
        {
            _tcs.SetResult(null);
        }
    }
    
    public override void DidComplete(ASAuthorizationController controller, NSError error)
    {
        _tcs.SetException(new Exception(error.LocalizedDescription));
    }
}
```

### Android Implementation

For Android, use a WebView-based implementation or third-party library:

```kotlin
// Using web-based flow
fun signInWithApple() {
    val authUrl = "https://appleid.apple.com/auth/authorize?" +
        "client_id=com.yourcompany.ben.api&" +
        "redirect_uri=https://yourapp.com/callback&" +
        "response_type=code%20id_token&" +
        "scope=name%20email&" +
        "response_mode=form_post"
    
    // Open in WebView or Custom Tab
    // Handle callback to extract ID token
}
```

### Web-Based Flow (All Platforms)

For web-based or hybrid apps, use Apple's JavaScript SDK:

```html
<script src="https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/en_US/appleid.auth.js"></script>

<script>
AppleID.auth.init({
    clientId: 'com.yourcompany.ben.api',
    scope: 'name email',
    redirectURI: 'https://yourapp.com/callback',
    usePopup: true
});

// Sign in
document.getElementById('appleid-signin').addEventListener('click', () => {
    AppleID.auth.signIn().then(response => {
        // response.authorization.id_token is your JWT
        const idToken = response.authorization.id_token;
        sendTokenToAPI(idToken);
    });
});
</script>

<div id="appleid-signin" 
     data-color="black" 
     data-border="true" 
     data-type="sign in">
</div>
```

## Sending Token to API

Regardless of platform, send the Apple ID token the same way as other providers:

```csharp
public class AuthenticationHandler : DelegatingHandler
{
    private readonly IAuthenticationService _authService;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Get token from Microsoft, Google, or Apple
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

## User Identity

### Apple User IDs

Apple provides a unique, stable user identifier in the `sub` claim:
- Format: String (e.g., `"001234.567890abcdef123456789.1234"`)
- **Unique per Services ID**: Same user gets different IDs for different Services IDs
- **Stable**: Doesn't change for a user
- **Opaque**: Not related to email or personal information

### Cross-Provider Identity

Just like Microsoft and Google:
- Each provider assigns different user IDs
- Apple ID: `"001234.567890abcdef123456789.1234"`
- Microsoft ID: `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
- Google ID: `123456789012345678901`
- **Different IDs = Different users** (even with same email)

### Privacy Considerations

Apple is especially privacy-focused:
- Users can choose to hide their email (use relay address)
- Email relay: `privaterelay@icloud.com` forwards to user's real email
- User name may be withheld on subsequent sign-ins
- Respect user's privacy choices in your UI

## Testing

### Testing Apple Sign In

1. **Get Test Token**:
   - Use Apple Sign In on a device/simulator
   - Capture the ID token from the response

2. **Decode Token**:
   - Go to https://jwt.ms and paste the token
   - Verify:
     - `iss`: `https://appleid.apple.com`
     - `aud`: Your Services ID (e.g., `com.yourcompany.ben.api`)
     - `sub`: User's unique Apple ID

3. **Test API Call**:
   ```bash
   curl -H "Authorization: ******" \
        https://your-api-url/tables/noteitem
   ```

### Expected Results

- **With valid Apple token**: 200 OK, returns user's data
- **With valid token from other provider**: 200 OK, returns different user's data
- **Without token**: 401 Unauthorized
- **With invalid token**: 401 Unauthorized
- **With expired token**: 401 Unauthorized

## Troubleshooting

### "Invalid issuer"

**Problem**: Apple token issuer not recognized.

**Solution**: 
- Verify issuer in token is exactly `https://appleid.apple.com`
- Check server configuration includes this issuer in `ValidIssuers`

### "Invalid audience"

**Problem**: Token audience doesn't match configured Services ID.

**Solution**:
- Decode token and check `aud` claim
- Ensure it matches your Services ID in `AppleAuth:ClientId`
- Services ID must match exactly (case-sensitive)

### "Private email relay"

**Situation**: User chose to hide email, you get `privaterelay@icloud.com`.

**Solution**: This is normal and expected. Apple forwards emails to the user's real address. Your app should handle relay addresses properly.

### "No email or name on second sign-in"

**Situation**: User information only provided on first sign-in.

**Solution**: Apple only provides email/name on first authorization. Cache this information locally or ask Apple for it again by forcing re-authentication.

### "Services ID vs App ID confusion"

**Problem**: Using App ID instead of Services ID as ClientId.

**Solution**:
- **App ID**: Used for app capabilities (e.g., `com.yourcompany.ben`)
- **Services ID**: Used for authentication (e.g., `com.yourcompany.ben.api`)
- Server needs the **Services ID**, not the App ID

## Platform Requirements

### iOS / MacCatalyst

- **Minimum**: iOS 13.0+, macOS 10.15+
- **Framework**: AuthenticationServices
- **Required Capability**: Sign in with Apple
- **Bundle ID**: Must match App ID in developer console

### Android

- **Web-based flow required** (Apple doesn't provide native Android SDK)
- Use WebView or Chrome Custom Tabs
- Handle OAuth redirect properly

### Web

- **JavaScript SDK**: Apple provides official JS library
- **HTTPS required**: Redirect URIs must use HTTPS
- **Domain verification**: Register domain in Services ID configuration

## Security Considerations

### Token Validation

- ✅ **Issuer validation**: Only `https://appleid.apple.com` accepted
- ✅ **Audience validation**: Only your Services ID accepted
- ✅ **Signature validation**: Using Apple's public keys
- ✅ **Expiration validation**: Tokens expire (typically 10 minutes)

### Privacy Features

- **Email hiding**: Respect user's choice to hide email
- **Private relay**: Handle relay email addresses
- **Data minimization**: Only request scopes you need
- **User control**: Users can revoke access via Apple ID settings

### Best Practices

1. **Request minimal scopes**: Only ask for name/email if truly needed
2. **Handle privacy relay**: Support `privaterelay@icloud.com` addresses
3. **Cache user info**: Email/name only provided on first sign-in
4. **Test on device**: Simulator may not fully support Sign in with Apple
5. **Handle revocation**: User can revoke access at any time

## Apple-Specific Features

### Sign in with Apple Button

Apple requires specific button styling:
- Use Apple's official button design
- Available in JavaScript SDK or native APIs
- Guidelines: https://developer.apple.com/design/human-interface-guidelines/sign-in-with-apple

### Transfer App ID

If you already have users with another authentication method and want to migrate to Apple Sign In:
1. User must re-authenticate with Apple
2. Link accounts based on email (if available)
3. Consider account linking UI

### Two-Factor Authentication

Apple automatically handles 2FA:
- Users may be prompted for 2FA during sign-in
- Transparent to your app
- No additional implementation needed

## Advanced Configuration

### Multiple Services IDs

If you have multiple apps or environments:

```json
{
  "AppleAuth": {
    "ClientId": "com.yourcompany.ben.api",
    "ClientIds": [
      "com.yourcompany.ben.api",
      "com.yourcompany.ben.api.dev",
      "com.yourcompany.ben.api.staging"
    ]
  }
}
```

Update `ValidAudiences` to include all Services IDs.

### Team ID Validation (Optional)

For additional security, validate the team ID:

```csharp
// In token validation
var teamId = user.FindFirst("https://appleid.apple.com/team_id")?.Value;
if (teamId != "YOUR_TEAM_ID") 
{
    // Reject token
}
```

## Resources

- **Apple Documentation**: https://developer.apple.com/sign-in-with-apple/
- **Human Interface Guidelines**: https://developer.apple.com/design/human-interface-guidelines/sign-in-with-apple
- **OpenID Configuration**: https://appleid.apple.com/.well-known/openid-configuration
- **Apple Developer Console**: https://developer.apple.com/account/

## Summary

Apple ID authentication is now fully supported:
- ✅ Server validates Apple tokens
- ✅ Uses standard OpenID Connect
- ✅ Same flow as Microsoft and Google
- ✅ Privacy-focused (email hiding, relay addresses)
- ✅ Requires Apple Developer Account
- ✅ Native support on iOS/Mac, web-based on Android

Configure your Services ID, add it to `appsettings.json`, and implement client-side Sign in with Apple using Apple's official SDKs.
