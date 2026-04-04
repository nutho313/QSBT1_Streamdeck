# ============================================================
# build-plugin.ps1
# Builds the QSBT1 Stream Deck plugin and creates a
# ready-to-install .streamDeckPlugin file
# Usage: .\build-plugin.ps1
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
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  nutho313 QSBT1 Plugin Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ── 1. Build ─────────────────────────────────────────────────
Write-Host "[1/4] Building project (Release)..." -ForegroundColor Yellow
dotnet build "$ProjectDir\QSBT1_Streamdeck.csproj" -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Host "Build FAILED!" -ForegroundColor Red; exit 1 }
Write-Host "      Build OK" -ForegroundColor Green

# ── 2. Prepare output folder ─────────────────────────────────
Write-Host "[2/4] Preparing plugin folder..." -ForegroundColor Yellow
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item -ItemType Directory -Path $PackageDir | Out-Null

# Copy all build output
Copy-Item "$BuildDir\*" -Destination $PackageDir -Recurse -Force

# Copy manifest + pi folder from project root
Copy-Item "$ProjectDir\manifest.json" -Destination $PackageDir -Force
if (Test-Path "$ProjectDir\pi") {
    Copy-Item "$ProjectDir\pi" -Destination $PackageDir -Recurse -Force
}
if (Test-Path "$ProjectDir\images") {
    Copy-Item "$ProjectDir\images" -Destination $PackageDir -Recurse -Force
}

# Copy native SkiaSharp DLL to root (required)
$nativeDll = "$PackageDir\runtimes\win-x64\native\libSkiaSharp.dll"
if (Test-Path $nativeDll) {
    Copy-Item $nativeDll -Destination $PackageDir -Force
    Write-Host "      Copied libSkiaSharp.dll to root" -ForegroundColor Gray
}

# Create QSBT1_Streamdeck.dll alias (exe looks for it)
$mainDll  = "$PackageDir\nutho313.QSBT1_Streamdeck.dll"
$aliasDll = "$PackageDir\QSBT1_Streamdeck.dll"
if ((Test-Path $mainDll) -and -not (Test-Path $aliasDll)) {
    Copy-Item $mainDll -Destination $aliasDll -Force
    Write-Host "      Created QSBT1_Streamdeck.dll alias" -ForegroundColor Gray
}

Write-Host "      Plugin folder ready" -ForegroundColor Green

# ── 3. Create .streamDeckPlugin ──────────────────────────────
Write-Host "[3/4] Creating .streamDeckPlugin..." -ForegroundColor Yellow
if (Test-Path $PluginFile) { Remove-Item $PluginFile -Force }

$tempZip = "$OutputDir\temp.zip"
Compress-Archive -Path $PackageDir -DestinationPath $tempZip -Force
Rename-Item $tempZip $PluginFile
Write-Host "      Created: $PluginFile" -ForegroundColor Green

# ── 4. Install locally ───────────────────────────────────────
Write-Host "[4/4] Installing to Stream Deck plugins folder..." -ForegroundColor Yellow
$sdPluginsDir = "$env:APPDATA\Elgato\StreamDeck\Plugins\$PluginFolder"
if (Test-Path $sdPluginsDir) { Remove-Item $sdPluginsDir -Recurse -Force }
Copy-Item $PackageDir -Destination $sdPluginsDir -Recurse -Force
Write-Host "      Installed to: $sdPluginsDir" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DONE!" -ForegroundColor Green
Write-Host "  Plugin file : releases\$PluginId.streamDeckPlugin" -ForegroundColor White
Write-Host "  Restart Stream Deck to load the plugin" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
