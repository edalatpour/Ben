# Account Deletion Test Checklist

Use this checklist before release to validate Delete Cloud Data behavior.

## Preconditions
- App is built from current branch.
- Server endpoint `POST /account/delete-cloud-data` is deployed.
- Test account has known cloud data across tasks, notes, and projects.
- Device has local data present before test starts.

## Core Flow (Happy Path)
1. Sign in.
2. Open Settings.
3. Tap Delete cloud data (Delete account).
4. Complete re-authentication prompt.
5. Confirm destructive prompt.
6. Verify app signs out automatically.
7. Verify signed-out notice says local data remains on device.
8. Verify local data is still visible.

Expected:
- Success dialog shows deleted counts.
- User is signed out regardless of cloud result.
- Local data remains on device.

## Failure Handling
1. Repeat flow with network disabled before delete request.
2. Complete re-auth and confirm deletion.

Expected:
- User is still signed out.
- Failure dialog includes status/details.
- Local data remains on device.

## Re-Auth Gate
1. Start deletion flow.
2. Cancel re-authentication.

Expected:
- No cloud deletion attempted.
- User remains signed in.
- Message says cloud data was not deleted.

## Re-Sign In Behavior
1. After successful cloud deletion and sign-out, sign in again with the same account.
2. Trigger sync (or wait for auto sync).

Expected:
- Local data syncs to cloud for that account.

3. Sign out, then sign in with a different account.
4. Trigger sync.

Expected:
- Local data syncs under the new account scope.

## Regression Checks
- Sign out button does not delete local data.
- Delete local data still requires confirmation.
- Delete local data refreshes current page immediately.
- App startup and normal sync behavior unchanged.
