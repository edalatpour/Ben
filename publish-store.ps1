# publish-store.ps1
# Builds MSIX packages for both x64 and ARM64 for Microsoft Store submission.
# Run from the solution root: .\publish-store.ps1

$project = "Ben.Client\Ben.Client.csproj"
$tfm     = "net10.0-windows10.0.19041.0"

$profiles = @(
    @{ Name = "MSIX-win-x64-store";   OutDir = "Ben.Client\bin\Release\$tfm\win-x64\AppPackages" },
    @{ Name = "MSIX-win-arm64-store"; OutDir = "Ben.Client\bin\Release\$tfm\win-arm64\AppPackages" }
)

foreach ($p in $profiles) {
    Write-Host ""
    Write-Host "Publishing with profile $($p.Name)..." -ForegroundColor Cyan

    dotnet publish $project -f $tfm -c Release /p:PublishProfile=$($p.Name)

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: publish failed for profile $($p.Name)." -ForegroundColor Red
        exit 1
    }

    Write-Host "Publish succeeded for $($p.Name)." -ForegroundColor Green
}

Write-Host ""
Write-Host "Opening output folders..." -ForegroundColor Yellow

foreach ($p in $profiles) {
    $fullPath = Join-Path (Get-Location) $p.OutDir
    if (Test-Path $fullPath) {
        Invoke-Item $fullPath
    }
}

Write-Host "Done. Upload both .msix files from the opened AppPackages folders to Partner Center." -ForegroundColor Yellow
