# Microsoft Authentication Setup Guide

## What Was Implemented

I've successfully added Microsoft account authentication to your Ben app. Here's what was done:

### 1. Added Microsoft Authentication Library (MSAL)
- Added `Microsoft.Identity.Client` NuGet package (v4.66.2)
- This library handles all OAuth 2.0 authentication flows with Microsoft identity platform

### 2. Created Authentication Service
- **File**: `Ben.Client/Services/AuthenticationService.cs`
- **Features**:
  - Sign in with Microsoft account using interactive authentication
  - Sign out and clear authentication state
  - Persistent authentication state using MAUI Preferences
  - Token cache serialization for offline access
  - Platform-specific configuration for Windows, iOS, macOS, and Android

### 3. Updated UI and ViewModel
- **SettingsViewModel**: Now integrates with AuthenticationService
  - `SignInCommand`: Authenticates user with Microsoft account
  - `SignOutCommand`: Logs out and clears cached credentials
  - Stores and displays user email and name
- **SettingsPage.xaml**: Shows/hides buttons based on authentication state
  - Sign In button (visible when not authenticated)
  - Sign Out button (visible when authenticated)
  - User info display showing signed-in email

### 4. Added UI Converter
- **InvertedBoolConverter**: Toggles button visibility based on sign-in state

### 5. Platform-Specific Configuration
- **iOS/MacCatalyst**: Added URL scheme `msauth.com.edalatpour.ben` to Info.plist
- **Android**: Added BrowserTabActivity for authentication callback
- **Windows**: Uses native Windows authentication broker

## What You Need To Configure

### Step 1: Register Your App in Microsoft Entra ID

1. Go to [Microsoft Entra admin center](https://entra.microsoft.com/) and sign in
2. Navigate to **Entra ID** (in left sidebar) > **App registrations**
3. Click **New registration**
4. Fill in the registration details:
   - **Name**: `Ben Daily Planner` (or your preferred name)
   - **Supported account types**: Select "**Accounts in any organizational directory and personal Microsoft accounts**"
   - **Redirect URI**: Leave blank for now (we'll add platform-specific ones in Step 2)
5. Click **Register**
6. **Copy the Application (client) ID** - you'll need this in Step 3

### Step 2: Configure Platform-Specific Redirect URIs

Once registration is complete, you'll be on the app's Overview page. Now add redirect URIs for each platform:

**Important**: The app uses the **system browser** (OS default) for authentication, which requires loopback redirect URIs on Windows.

#### For Windows:
1. Click **Authentication** in the left menu under "Manage"
2. Click **Add a platform** button
3. Select **Mobile and desktop applications**
4. Check the redirect URI: `http://localhost`
   - This is a loopback URI that the system browser uses for receiving the authentication response
5. Click **Configure**

#### For iOS/MacCatalyst:
1. Click **Authentication** in the left menu under "Manage"
2. Click **Add a platform** button
3. Select **Mobile and desktop applications**
4. Check the box for: `msauth.com.edalatpour.ben://auth`
5. Click **Configure**

#### For Android:
1. Click **Authentication** in the left menu under "Manage"
2. Click **Add a platform** button
3. Select **Mobile and desktop applications**
4. You'll need the Signature Hash:
   - Generate Signature Hash (run in PowerShell):
   ```powershell
   keytool -exportcert -alias androiddebugkey -keystore ~/.android/debug.keystore | openssl sha1 -binary | openssl base64
   ```
   - Default keystore password: `android`
5. In the app registration, add: `msauth://com.edalatpour.ben/YOUR_SIGNATURE_HASH`
6. Copy this hash - you'll need it in Step 3

### Step 3: Update Your Code

Update the following placeholders in `AuthenticationService.cs`:

#### Line 8 - Add Your Client ID:
Replace `d5a4dd1f-e90b-4c48-8031-15041bd3c02c` with the Application (client) ID you copied in Step 1:
```csharp
private const string ClientId = "YOUR_CLIENT_ID"; // e.g., "12345678-1234-1234-1234-123456789012"
```

#### Line 31 (Android only) - Add Your Signature Hash:
Replace `YOUR_SIGNATURE_HASH` with the hash you generated in Step 2:
```csharp
.WithRedirectUri("msauth://com.edalatpour.ben/YOUR_SIGNATURE_HASH"); // e.g., "msauth://com.edalatpour.ben/W/qw+L..."
```

Also update `Ben.Client/Platforms/Android/AndroidManifest.xml` line 11 with the same hash:
```xml
<data android:scheme="msauth"
      android:host="com.edalatpour.ben"
      android:path="/YOUR_SIGNATURE_HASH" />
```

### Step 4: Test the Authentication

1. Build and run your app
2. Navigate to Settings
3. Click "Sign in with Microsoft"
4. Complete the authentication flow in the browser/system dialog
5. You should see your email displayed
6. Your authentication state will persist even after closing the app
7. Click "Sign Out" to log out

## How It Works

### Authentication Flow:
1. User clicks "Sign in with Microsoft"
2. MSAL launches platform-specific web view or browser
3. User enters Microsoft credentials
4. Microsoft returns authentication token
5. Token is cached securely on device
6. App stores authentication state in MAUI Preferences
7. On next app launch, authentication state is restored

### Token Management:
- Tokens are cached in: `{AppDataDirectory}/msalcache.bin`
- MSAL automatically refreshes expired tokens
- Silent authentication attempts happen first (no UI shown)
- Interactive authentication only when necessary

### Platform-Specific Behavior:
- **Windows**: Uses the system default browser (Edge, Chrome, Firefox, etc.) via loopback redirect URI (`http://localhost`). MSAL automatically opens the browser, user signs in, and the response is received via localhost.
- **iOS/MacCatalyst**: Uses Safari View Controller or system browser with custom URL scheme
- **Android**: Uses Chrome Custom Tabs

## Security Notes

- Never commit your Client ID to public repositories
- The current setup only requests the `User.Read` scope (basic profile information)
- Authentication state is stored securely using platform-specific secure storage
- Tokens are encrypted by MSAL
- Token cache is encrypted and stored locally

## Troubleshooting

### "Only loopback redirect uri is supported" Error:
- **Cause**: The app is using the system browser, which only accepts loopback URIs like `http://localhost`
- **Solution**: Ensure you configured `http://localhost` in Entra ID, not `https://login.microsoftonline.com/common/oauth2/nativeclient`
- **Fix**: Update your app registration redirect URI to `http://localhost`

### "Invalid Client" Error:
- Verify your Client ID is correct and matches the one in Entra ID
- Ensure Windows redirect URI is set to `http://localhost` (not the nativeclient URI)
- Check that you're using the right account type (personal Microsoft accounts must be included)

### Android Authentication Fails:
- Verify your Signature Hash is correct
- Ensure it matches exactly in both code and the app registration
- The hash is case-sensitive
- Verify the keystore password is correct (default: `android`)

### iOS/macOS "Not Registered" Error:
- Verify Bundle ID matches: `com.edalatpour.ben`
- Check Info.plist has correct URL scheme: `msauth.com.edalatpour.ben`
- Ensure the redirect URI in the app registration exactly matches

### Windows Authentication Fails:
- Verify the Windows redirect URI in the app registration
- Try using `https://login.microsoftonline.com/common/oauth2/nativeclient`
- For WAM (Windows Account Manager), ensure you're on a supported Windows version

### Token Expired or Invalid:
- MSAL automatically handles token refresh
- If issues persist, sign out and sign in again
- Clear the token cache at `{AppDataDirectory}/msalcache.bin` if corruption is suspected

## Next Steps

After completing the configuration:
1. You can enhance the sync service to use the authentication token
2. Add token to API requests via `Authorization: Bearer {token}`
3. Implement token-based server-side authorization
4. Consider adding additional Microsoft Graph API calls (calendar, OneDrive, profile data, etc.)
5. Implement token refresh handling for long-running operations

## Files Modified/Created

### Created:
- `Ben.Client/Services/AuthenticationService.cs`
- `Ben.Client/Converters/InvertedBoolConverter.cs`
- `AUTHENTICATION_SETUP.md` (this file)

### Modified:
- `Ben.Client/Ben.Client.csproj` (added MSAL package)
- `Ben.Client/MauiProgram.cs` (registered auth service)
- `Ben.Client/ViewModels/SettingsViewModel.cs` (added auth logic)
- `Ben.Client/Views/SettingsPage.xaml` (updated UI)
- `Ben.Client/App.xaml` (added converter resource)
- `Ben.Client/Platforms/iOS/Info.plist` (added URL scheme)
- `Ben.Client/Platforms/MacCatalyst/Info.plist` (added URL scheme)
- `Ben.Client/Platforms/Android/AndroidManifest.xml` (added browser activity)

## Additional Resources

- [Microsoft Entra ID Documentation](https://learn.microsoft.com/en-us/entra/identity/)
- [Register an application in Microsoft Entra ID](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [Desktop app authentication configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-registration)
- [MSAL.NET Documentation](https://learn.microsoft.com/en-us/entra/msal/dotnet/)
- [Microsoft identity platform](https://learn.microsoft.com/en-us/entra/identity-platform/)

