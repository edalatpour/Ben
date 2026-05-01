param(
  [string]$ProjectPath = "./Ben.Client/Ben.Client.csproj",
  [string]$Configuration = "Release",
  [string]$RuntimeIdentifier = "ios-arm64",
  [string]$TeamId = "QNQ323Z7FW",
  [string]$BundleId = "com.edalatpour.ben",
  [string]$CodesignKey = "iPhone Distribution: Tim Edalatpour (QNQ323Z7FW)",
  [string]$CodesignProvision = "BenPlannerAppStore",
  [string]$OutputDir = "./publish"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectFullPath = Resolve-Path (Join-Path $repoRoot $ProjectPath)
$outputFullPath = Join-Path $repoRoot $OutputDir

if (-not (Get-Command xcodebuild -ErrorAction SilentlyContinue)) {
  throw "xcodebuild not found. Install Xcode command line tools and try again."
}

Write-Host "Building iOS archive..."
$buildArgs = @(
  "build"
  "-f", "net10.0-ios"
  "-c", $Configuration
  "-p:RuntimeIdentifier=$RuntimeIdentifier"
  "-p:ArchiveOnBuild=true"
  "-p:CodesignKey=$CodesignKey"
  "-p:CodesignProvision=$CodesignProvision"
  $projectFullPath
)

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
  throw "dotnet build failed with exit code $LASTEXITCODE"
}

$archiveRoot = Join-Path $HOME "Library/Developer/Xcode/Archives"
$latestArchive = Get-ChildItem -Path $archiveRoot -Filter *.xcarchive -Recurse |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

if (-not $latestArchive) {
  throw "No .xcarchive found under $archiveRoot"
}

Write-Host "Using archive: $($latestArchive.FullName)"

New-Item -ItemType Directory -Path $outputFullPath -Force | Out-Null

$exportOptionsPlist = Join-Path ([System.IO.Path]::GetTempPath()) "ExportOptions-Ben.plist"
@"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key>
  <string>app-store-connect</string>
  <key>teamID</key>
  <string>$TeamId</string>
  <key>provisioningProfiles</key>
  <dict>
    <key>$BundleId</key>
    <string>$CodesignProvision</string>
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
"@ | Set-Content -Path $exportOptionsPlist -Encoding utf8

Write-Host "Exporting IPA..."
& xcodebuild -exportArchive `
  -archivePath $latestArchive.FullName `
  -exportPath $outputFullPath `
  -exportOptionsPlist $exportOptionsPlist

if ($LASTEXITCODE -ne 0) {
  throw "xcodebuild -exportArchive failed with exit code $LASTEXITCODE"
}

$ipa = Get-ChildItem -Path $outputFullPath -Filter *.ipa | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $ipa) {
  throw "Export succeeded but no .ipa found in $outputFullPath"
}

Write-Host "IPA ready: $($ipa.FullName)"