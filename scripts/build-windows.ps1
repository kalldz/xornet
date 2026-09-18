#!/usr/bin/env pwsh
# Build script for Windows - creates self-contained executable

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [string]$OutputPath = "publish\windows"
)

$ErrorActionPreference = "Stop"

Write-Host "Building Xornet for Windows..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Runtime: $Runtime" -ForegroundColor Gray

# Clean output
if (Test-Path $OutputPath) {
    Remove-Item -Recurse -Force $OutputPath
}

# Publish self-contained
$publishArgs = @(
    "publish"
    "src\Xornet\Xornet.csproj"
    "-c", $Configuration
    "-r", $Runtime
    "--self-contained", "true"
    "-p:PublishSingleFile=true"
    "-p:IncludeNativeLibrariesForSelfExtract=true"
    "-p:DebugType=None"
    "-p:DebugSymbols=false"
    "-o", $OutputPath
)

dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Copy assets
$assetsSource = "assets"
$assetsDest = Join-Path $OutputPath "assets"
if (Test-Path $assetsSource) {
    Copy-Item -Recurse -Force $assetsSource $assetsDest
}

# Create ZIP
$zipName = "Xornet-$Runtime.zip"
Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipName -Force

Write-Host "" -ForegroundColor Green
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $OutputPath" -ForegroundColor Green
Write-Host "Archive: $zipName" -ForegroundColor Green
