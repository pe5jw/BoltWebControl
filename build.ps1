# BoltWebControl build script
# Gebruik: .\build.ps1               → alles bouwen
#          .\build.ps1 -Run web      → BoltWebControl starten
#          .\build.ps1 -Run audio    → BoltAudioBridge starten

param([string]$Run = "")

$ErrorActionPreference = "Stop"
$slnRoot   = $PSScriptRoot
$webProj   = "src\BoltWebControl\BoltWebControl.csproj"
$audioProj = "src\BoltAudioBridge\BoltAudioBridge.csproj"

function Build($proj, $label) {
    Write-Host "`n=== $label bouwen ===" -ForegroundColor Cyan
    dotnet build $proj -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD MISLUKT: $label" -ForegroundColor Red
        exit 1
    }
    Write-Host "$label build geslaagd ✓" -ForegroundColor Green
}

Build $webProj   "BoltWebControl"
Build $audioProj "BoltAudioBridge"

Write-Host "`nAlle builds geslaagd ✓" -ForegroundColor Green

switch ($Run.ToLower()) {
    "web"   { Write-Host "=== BoltWebControl starten op http://localhost:5090 ===" -ForegroundColor Cyan
              dotnet run --project $webProj --no-build }
    "audio" { Write-Host "=== BoltAudioBridge starten op http://localhost:5080 ===" -ForegroundColor Cyan
              dotnet run --project $audioProj --no-build }
}
