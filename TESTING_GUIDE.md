# Testing Guide for Multi-Provider Authentication

This guide provides step-by-step instructions for testing the Microsoft, Google, and Apple authentication implementations before merging.

## Overview

The authentication system validates JWT tokens from three providers:
- Microsoft (Personal & Work/School accounts)
- Google (Gmail & Workspace accounts)  
- Apple ID (Sign in with Apple)

## Testing Approach

You can test authentication changes at multiple levels:

1. **Unit Testing** - Token validation logic
2. **Integration Testing** - Full authentication flow
3. **Manual Testing** - Using real tokens from providers

## Prerequisites

Before testing, ensure you have:
- ✅ .NET 10 SDK installed
- ✅ SQL Server LocalDB or SQL Server instance
- ✅ Access to Azure Portal (for Microsoft testing)
- ✅ Access to Google Cloud Console (for Google testing)
- ✅ Access to Apple Developer Console (for Apple testing)

## Option 1: Quick Validation (No Provider Setup Required)

### Step 1: Verify Build

```bash
cd Ben.Datasync.Server
dotnet build
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 2: Verify Configuration

Check that all providers are configured:

```bash
# View configuration
cat appsettings.json
```

**Expected Output:**
```json
{
  "AzureAd": { "ClientId": "..." },
  "GoogleAuth": { "ClientId": "..." },
  "AppleAuth": { "ClientId": "..." }
}
```

### Step 3: Run the Server

```bash
cd Ben.Datasync.Server
dotnet run
```

**Expected Output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
      Now listening on: http://localhost:5000
```

### Step 4: Test Unauthenticated Request

```bash
curl -v http://localhost:5000/tables/noteitem
```

**Expected Result:** `401 Unauthorized`

This confirms authentication is required.

## Option 2: Test with Mock JWT Tokens

You can create mock JWT tokens for testing without setting up real providers.

### Step 1: Create Test Tokens

Use https://jwt.io to create test tokens with:

**Microsoft Token:**
```json
{
  "iss": "https://login.microsoftonline.com/common/v2.0",
  "aud": "your-test-client-id",
  "sub": "test-user-microsoft-123",
  "exp": 9999999999
}
```

**Google Token:**
```json
{
  "iss": "https://accounts.google.com",
  "aud": "your-test-client-id",
  "sub": "test-user-google-456",
  "exp": 9999999999
}
```

**Apple Token:**
```json
{
  "iss": "https://appleid.apple.com",
  "aud": "com.test.app",
  "sub": "test-user-apple-789",
  "exp": 9999999999
}
```

**Note:** These mock tokens won't validate signatures, but you can test the configuration.

## Option 3: Test with Real Tokens (Recommended)

### Testing Microsoft Authentication

#### Setup (5 minutes)

1. **Create Azure AD App Registration**
   ```
   Go to: https://portal.azure.com
   Navigate: Azure Active Directory > App registrations > New registration
   Name: Ben Test App
   Account types: Personal Microsoft accounts only
   Register
   ```

2. **Get Client ID**
   ```
   Copy: Application (client) ID
   Update: appsettings.json > AzureAd:ClientId
   ```

3. **Configure Scopes**
   ```
   Go to: Expose an API
   Add scope: access_as_user
   Who can consent: Admins and users
   ```

#### Test Flow

1. **Get a token using PowerShell/Azure CLI:**

   ```powershell
   # Install Azure CLI if needed
   # https://docs.microsoft.com/en-us/cli/azure/install-azure-cli
   
   az login --allow-no-subscriptions
   
   # Get token for your client ID
   $token = az account get-access-token --resource "api://YOUR-CLIENT-ID" --query accessToken -o tsv
   echo $token
   ```

   **Or use MSAL in a test app:**
   
   ```csharp
   var pca = PublicClientApplicationBuilder
       .Create("YOUR-CLIENT-ID")
       .WithAuthority(AzureCloudInstance.AzurePublic, "common")
       .WithRedirectUri("http://localhost")
       .Build();
   
   var result = await pca.AcquireTokenInteractive(
       new[] { "api://YOUR-CLIENT-ID/access_as_user" })
       .ExecuteAsync();
   
   Console.WriteLine(result.AccessToken);
   ```

2. **Decode token to verify:**
   ```
   Go to: https://jwt.ms
   Paste token
   Verify: iss = https://login.microsoftonline.com/...
   Verify: aud = YOUR-CLIENT-ID
   Verify: sub = user ID
   ```

3. **Test API call:**
   ```bash
   curl -H "Authorization: ******" \
        http://localhost:5000/tables/noteitem
   ```

   **Expected:** 200 OK (empty array if no data)

### Testing Google Authentication

#### Setup (5 minutes)

1. **Create Google OAuth Client**
   ```
   Go to: https://console.cloud.google.com
   Create project or select existing
   Enable: Google+ API or Google Identity
   Create credentials: OAuth 2.0 Client ID
   Application type: Web application or Desktop
   ```

2. **Get Client ID**
   ```
   Copy: Client ID (xxx.apps.googleusercontent.com)
   Update: appsettings.json > GoogleAuth:ClientId
   ```

#### Test Flow

1. **Get a token using OAuth Playground:**
   ```
   Go to: https://developers.google.com/oauthplayground
   Click: Settings (gear icon)
   Check: Use your own OAuth credentials
   Enter: Your Client ID and Client Secret
   
   In left panel:
   Select scope: openid
   Click: Authorize APIs
   Sign in with Google account
   
   Click: Exchange authorization code for tokens
   Copy: id_token (this is your JWT)
   ```

2. **Decode token:**
   ```
   Go to: https://jwt.ms
   Paste id_token
   Verify: iss = https://accounts.google.com
   Verify: aud = YOUR-CLIENT-ID.apps.googleusercontent.com
   Verify: sub = Google user ID
   ```

3. **Test API call:**
   ```bash
   curl -H "Authorization: ******" \
        http://localhost:5000/tables/noteitem
   ```

   **Expected:** 200 OK (empty array)

### Testing Apple Authentication

#### Setup (10 minutes - requires Apple Developer account)

1. **Create App ID**
   ```
   Go to: https://developer.apple.com/account
   Certificates, Identifiers & Profiles
   Identifiers > App IDs > Register
   Enable: Sign in with Apple
   ```

2. **Create Services ID**
   ```
   Identifiers > Services IDs > Register
   Identifier: com.yourcompany.ben.api
   Enable: Sign in with Apple
   Configure: Add domain and return URL
   ```

3. **Update Configuration**
   ```
   Update: appsettings.json > AppleAuth:ClientId
   Value: com.yourcompany.ben.api (your Services ID)
   ```

#### Test Flow

1. **Get a token (requires iOS device or simulator):**

   ```swift
   // Run on iOS device/simulator
   import AuthenticationServices
   
   let provider = ASAuthorizationAppleIDProvider()
   let request = provider.createRequest()
   request.requestedScopes = [.email]
   
   let controller = ASAuthorizationController(authorizationRequests: [request])
   controller.performRequests()
   
   // In delegate:
   let idToken = String(data: credential.identityToken!, encoding: .utf8)
   print(idToken)
   ```

2. **Decode token:**
   ```
   Go to: https://jwt.ms
   Paste id_token
   Verify: iss = https://appleid.apple.com
   Verify: aud = com.yourcompany.ben.api
   Verify: sub = Apple user ID
   ```

3. **Test API call:**
   ```bash
   curl -H "Authorization: ******" \
        http://localhost:5000/tables/noteitem
   ```

   **Expected:** 200 OK (empty array)

## Option 4: Automated Testing Script

Create a test script to validate the server configuration:

### test-auth.sh

```bash
#!/bin/bash

echo "Testing Ben Datasync Server Authentication"
echo "==========================================="
echo ""

# Test 1: Server starts
echo "Test 1: Server starts"
cd Ben.Datasync.Server
timeout 10 dotnet run &
SERVER_PID=$!
sleep 5

if ps -p $SERVER_PID > /dev/null; then
    echo "✅ Server started successfully"
else
    echo "❌ Server failed to start"
    exit 1
fi

# Test 2: Unauthenticated request returns 401
echo ""
echo "Test 2: Unauthenticated request returns 401"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/tables/noteitem)

if [ "$HTTP_CODE" == "401" ]; then
    echo "✅ Correctly returns 401 Unauthorized"
else
    echo "❌ Expected 401, got $HTTP_CODE"
fi

# Test 3: Configuration includes all providers
echo ""
echo "Test 3: Configuration includes all providers"
if grep -q "AzureAd" appsettings.json && \
   grep -q "GoogleAuth" appsettings.json && \
   grep -q "AppleAuth" appsettings.json; then
    echo "✅ All provider configurations present"
else
    echo "❌ Missing provider configuration"
fi

# Cleanup
kill $SERVER_PID
echo ""
echo "Testing complete!"
```

Run the script:
```bash
chmod +x test-auth.sh
./test-auth.sh
```

## Option 5: Integration Testing with Postman

### Setup Postman Collection

1. **Import this collection:**

```json
{
  "info": {
    "name": "Ben Datasync Auth Testing",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Test Unauthenticated",
      "request": {
        "method": "GET",
        "header": [],
        "url": "http://localhost:5000/tables/noteitem"
      },
      "response": []
    },
    {
      "name": "Test Microsoft Auth",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "Authorization",
            "value": "******",
            "type": "text"
          }
        ],
        "url": "http://localhost:5000/tables/noteitem"
      }
    },
    {
      "name": "Test Google Auth",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "Authorization",
            "value": "******",
            "type": "text"
          }
        ],
        "url": "http://localhost:5000/tables/noteitem"
      }
    },
    {
      "name": "Test Apple Auth",
      "request": {
        "method": "GET",
        "header": [
          {
            "key": "Authorization",
            "value": "******",
            "type": "text"
          }
        ],
        "url": "http://localhost:5000/tables/noteitem"
      }
    }
  ]
}
```

2. **Test each request:**
   - First request should return 401
   - Other requests need real tokens (replace `{token}`)

## Verification Checklist

Before merging, verify:

- [ ] ✅ Server builds without errors
- [ ] ✅ Server starts without errors
- [ ] ✅ Unauthenticated requests return 401
- [ ] ✅ All three provider configs present in appsettings.json
- [ ] ✅ At least one provider tested with real token (recommended)
- [ ] ✅ Token from tested provider returns 200 OK
- [ ] ✅ Data isolation works (different user IDs see different data)
- [ ] ✅ No security vulnerabilities (CodeQL scan passed)
- [ ] ✅ Documentation is complete and accurate

## Quick Test (Minimum for Merge)

If you're short on time, do this minimum testing:

```bash
# 1. Build succeeds
cd Ben.Datasync.Server
dotnet build

# 2. Server starts
dotnet run &
sleep 5

# 3. Returns 401 without auth
curl -v http://localhost:5000/tables/noteitem 2>&1 | grep "401"

# 4. Configuration has all providers
grep -E "(AzureAd|GoogleAuth|AppleAuth)" appsettings.json

# If all pass, you're good to merge!
```

## Common Issues and Solutions

### Issue: Build fails
**Solution:** Run `dotnet restore` first

### Issue: Server won't start
**Solution:** Check SQL Server is running, connection string is correct

### Issue: Always returns 401 even with token
**Solution:** 
- Verify token audience matches ClientId in appsettings.json
- Check token isn't expired (decode at jwt.ms)
- Verify issuer is in ValidIssuers list

### Issue: Can't get real tokens
**Solution:** Use Option 1 (Quick Validation) or Option 4 (Automated Testing) - you don't need real tokens to verify the code changes are correct

## Recommended Testing Order

1. **Start Simple:** Option 1 (Quick Validation) - 2 minutes
2. **Add Automation:** Option 4 (Automated Testing) - 5 minutes  
3. **Pick One Provider:** Option 3 (Real Tokens) for Microsoft - 10 minutes
4. **Full Testing:** All three providers if needed - 30 minutes

## After Merge

After merging, you'll want to:
1. Update production `appsettings.json` with real client IDs
2. Test each provider in production environment
3. Monitor authentication logs for any issues
4. Update client applications to use new authentication options

## Need Help?

- Check documentation: `APPLE_AUTH_GUIDE.md`, `MULTI_PROVIDER_AUTH_GUIDE.md`
- Review JWT at: https://jwt.ms
- Debug tokens: Add logging in `Program.cs` event handlers
- Test OAuth flows: https://developers.google.com/oauthplayground

## Summary

**Minimum Testing Required:**
- ✅ Build succeeds
- ✅ Server starts
- ✅ Returns 401 without authentication

**Recommended Testing:**
- ✅ All of the above
- ✅ Test with at least one real provider token
- ✅ Verify token validation works

**Complete Testing:**
- ✅ All of the above  
- ✅ Test all three providers with real tokens
- ✅ Verify data isolation between users
- ✅ Test token expiration handling

Choose the testing level that matches your timeline and risk tolerance!
