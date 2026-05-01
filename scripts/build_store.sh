#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PROJECT_PATH="${PROJECT_PATH:-$REPO_ROOT/Ben.Client/Ben.Client.csproj}"
CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-ios-arm64}"
TEAM_ID="${TEAM_ID:-QNQ323Z7FW}"
BUNDLE_ID="${BUNDLE_ID:-com.edalatpour.ben}"
CODESIGN_KEY="${CODESIGN_KEY:-iPhone Distribution: Tim Edalatpour (QNQ323Z7FW)}"
CODESIGN_PROVISION="${CODESIGN_PROVISION:-BenPlannerAppStore}"
OUTPUT_DIR="${OUTPUT_DIR:-$REPO_ROOT/publish}"

if ! command -v xcodebuild >/dev/null 2>&1; then
  echo "xcodebuild not found. Install Xcode command line tools and try again." >&2
  exit 1
fi

echo "Building iOS archive..."
dotnet build \
  -f net10.0-ios \
  -c "$CONFIGURATION" \
  -p:RuntimeIdentifier="$RUNTIME_IDENTIFIER" \
  -p:ArchiveOnBuild=true \
  -p:CodesignKey="$CODESIGN_KEY" \
  -p:CodesignProvision="$CODESIGN_PROVISION" \
  "$PROJECT_PATH"

ARCHIVE_ROOT="$HOME/Library/Developer/Xcode/Archives"
LATEST_ARCHIVE="$(find "$ARCHIVE_ROOT" -name '*.xcarchive' -type d -print0 | xargs -0 ls -td | head -n 1 || true)"

if [[ -z "$LATEST_ARCHIVE" ]]; then
  echo "No .xcarchive found under $ARCHIVE_ROOT" >&2
  exit 1
fi

echo "Using archive: $LATEST_ARCHIVE"
mkdir -p "$OUTPUT_DIR"

EXPORT_OPTIONS_PLIST="${TMPDIR:-/tmp}/ExportOptions-Ben.plist"
cat > "$EXPORT_OPTIONS_PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key>
  <string>app-store-connect</string>
  <key>teamID</key>
  <string>$TEAM_ID</string>
  <key>provisioningProfiles</key>
  <dict>
    <key>$BUNDLE_ID</key>
    <string>$CODESIGN_PROVISION</string>
  </dict>
  <key>signingStyle</key>
  <string>manual</string>
  <key>signingCertificate</key>
  <string>iPhone Distribution</string>
  <key>uploadBitcode</key>
  <false/>
  <key>uploadSymbols</key>
  <true/>
</dict>
</plist>
EOF

echo "Exporting IPA..."
xcodebuild -exportArchive \
  -archivePath "$LATEST_ARCHIVE" \
  -exportPath "$OUTPUT_DIR" \
  -exportOptionsPlist "$EXPORT_OPTIONS_PLIST"

IPA_PATH="$(find "$OUTPUT_DIR" -maxdepth 1 -name '*.ipa' -type f -print0 | xargs -0 ls -t | head -n 1 || true)"
if [[ -z "$IPA_PATH" ]]; then
  echo "Export succeeded but no .ipa found in $OUTPUT_DIR" >&2
  exit 1
fi

echo "IPA ready: $IPA_PATH"
