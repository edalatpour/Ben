#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   scripts/force-deploy-ios.sh [device_id] [bundle_id]
# Example:
#   scripts/force-deploy-ios.sh 00008150-000619911A99401C com.edalatpour.Ben

DEVICE_ID="${1:-00008150-000619911A99401C}"
BUNDLE_ID="${2:-com.edalatpour.Ben}"

PROJECT_PATH="./Ben.Client/Ben.Client.csproj"
CONFIGURATION="Debug"
TFM="net10.0-ios"
RID="ios-arm64"
APP_PATH="./Ben.Client/bin/${CONFIGURATION}/${TFM}/${RID}/Ben.Client.app"

if ! command -v xcrun >/dev/null 2>&1; then
  echo "error: xcrun is not installed or not on PATH."
  exit 1
fi

echo "==> Building iOS app (${CONFIGURATION}, ${TFM}, ${RID})"
dotnet build \
  -t:Build \
  -p:Configuration="${CONFIGURATION}" \
  -f "${TFM}" \
  -r "${RID}" \
  -p:DisableXcodeValidationForLocalDebug=true \
  "${PROJECT_PATH}"

if [[ ! -d "${APP_PATH}" ]]; then
  echo "error: app bundle not found at ${APP_PATH}"
  exit 1
fi

echo "==> Installing app on device ${DEVICE_ID}"
xcrun devicectl device install app \
  --device "${DEVICE_ID}" \
  "${APP_PATH}"

echo "==> Launching ${BUNDLE_ID} on device ${DEVICE_ID}"
xcrun devicectl device process launch \
  --device "${DEVICE_ID}" \
  "${BUNDLE_ID}"

echo "==> Done"
