# BoltAudioBridge build + run script
# Gebruik: .\build.ps1          → alleen bouwen
#          .\build.ps1 -Run     → bouwen en starten

param([switch]$Run)

$ErrorActionPreference = "Stop"
$proj = "src\BoltAudioBridge\BoltAudioBridge.csproj"

Write-Host "=== BoltAudioBridge bouwen ===" -ForegroundColor Cyan
dotnet build $proj -c Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD MISLUKT" -ForegroundColor Red
    exit 1
}

Write-Host "Build geslaagd ✓" -ForegroundColor Green

if ($Run) {
    Write-Host "=== Starten op http://localhost:5080 ===" -ForegroundColor Cyan
    dotnet run --project $proj --no-build
}
