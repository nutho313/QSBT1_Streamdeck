# ============================================================
# build-plugin.ps1
# Usage: powershell -ExecutionPolicy Bypass -File C:\Projects\QSBT1_Streamdeck\build-plugin.ps1
# ============================================================

$ErrorActionPreference = "Stop"

$ProjectDir   = $PSScriptRoot
$BuildDir     = "$ProjectDir\bin\Release\net8.0"
$PluginId     = "ch.nutho313.qsbt1"
$PluginFolder = "$PluginId.sdPlugin"
$OutputDir    = "$ProjectDir\releases"
$PackageDir   = "$OutputDir\$PluginFolder"
$PluginFile   = "$OutputDir\$PluginId.streamDeckPlugin"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  nutho313 QSBT1 Plugin Builder" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# 0. Kill Stream Deck
Write-Host "[0/5] Stopping Stream Deck..." -ForegroundColor Yellow
$sdProcess = Get-Process -Name "StreamDeck" -ErrorAction SilentlyContinue
if ($sdProcess) {
    Stop-Process -Name "StreamDeck" -Force
    Start-Sleep -Seconds 2
    Write-Host "      Stopped" -ForegroundColor Green
} else {
    Write-Host "      Was not running" -ForegroundColor Gray
}

# 1. Build
Write-Host "[1/5] Building..." -ForegroundColor Yellow
dotnet build "$ProjectDir\QSBT1_Streamdeck.csproj" -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED!" -ForegroundColor Red
    exit 1
}
Write-Host "      Build OK" -ForegroundColor Green

# 2. Prepare plugin folder
Write-Host "[2/5] Preparing plugin folder..." -ForegroundColor Yellow
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item -ItemType Directory -Path $PackageDir | Out-Null
Copy-Item "$BuildDir\*" -Destination $PackageDir -Recurse -Force
Copy-Item "$ProjectDir\manifest.json" -Destination $PackageDir -Force
if (Test-Path "$ProjectDir\pi") {
    Copy-Item "$ProjectDir\pi" -Destination $PackageDir -Recurse -Force
}
if (Test-Path "$ProjectDir\images") {
    Copy-Item "$ProjectDir\images" -Destination $PackageDir -Recurse -Force
}
$nativeDll = "$PackageDir\runtimes\win-x64\native\libSkiaSharp.dll"
if (Test-Path $nativeDll) {
    Copy-Item $nativeDll -Destination $PackageDir -Force
}
$mainDll  = "$PackageDir\nutho313.QSBT1_Streamdeck.dll"
$aliasDll = "$PackageDir\QSBT1_Streamdeck.dll"
if ((Test-Path $mainDll) -and -not (Test-Path $aliasDll)) {
    Copy-Item $mainDll -Destination $aliasDll -Force
}
Write-Host "      Ready" -ForegroundColor Green

# 3. Create .streamDeckPlugin zip
Write-Host "[3/5] Creating .streamDeckPlugin..." -ForegroundColor Yellow
if (Test-Path $PluginFile) { Remove-Item $PluginFile -Force }
$tempZip = "$OutputDir\temp.zip"
Compress-Archive -Path $PackageDir -DestinationPath $tempZip -Force
Rename-Item $tempZip $PluginFile
Write-Host "      Created" -ForegroundColor Green

# 4. Install locally
Write-Host "[4/5] Installing..." -ForegroundColor Yellow
$sdPluginsDir = "$env:APPDATA\Elgato\StreamDeck\Plugins\$PluginFolder"
if (Test-Path $sdPluginsDir) { Remove-Item $sdPluginsDir -Recurse -Force }
Copy-Item $PackageDir -Destination $sdPluginsDir -Recurse -Force
Write-Host "      Installed" -ForegroundColor Green

# 5. Restart Stream Deck
Write-Host "[5/5] Restarting Stream Deck..." -ForegroundColor Yellow
$sdExe = "C:\Program Files\Elgato\StreamDeck\StreamDeck.exe"
if (Test-Path $sdExe) {
    Start-Process $sdExe
    Write-Host "      Restarted" -ForegroundColor Green
} else {
    Write-Host "      Not found - start manually" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  DONE!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
