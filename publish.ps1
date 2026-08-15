$ErrorActionPreference = "Stop"

# ------------------------------------------------------------
# Configuration
# ------------------------------------------------------------

$AppName = "firefox-shortcut"
$PublishDir = Join-Path $PSScriptRoot "publish"

# Automatically find the project file
$Projects = @(Get-ChildItem $PSScriptRoot -Filter "*.csproj" -Recurse)

if ($Projects.Count -eq 0) {
    throw "No .csproj file found."
}

if ($Projects.Count -gt 1) {
    Write-Host "Multiple .csproj files found:" -ForegroundColor Yellow
    $Projects | ForEach-Object { Write-Host "  $($_.FullName)" }
    throw "Please specify which project to publish."
}

$Project = $Projects[0].FullName

Write-Host "Project: $Project" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------
# Clean publish directory
# ------------------------------------------------------------

if (Test-Path $PublishDir) {
    Write-Host "Cleaning publish folder..." -ForegroundColor Yellow
    Remove-Item $PublishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $PublishDir | Out-Null

# ------------------------------------------------------------
# Find Visual Studio MSBuild
# ------------------------------------------------------------

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (-not (Test-Path $vswhere)) {
    throw "Could not find vswhere.exe. Is Visual Studio installed?"
}

$msbuild = & $vswhere `
    -latest `
    -products * `
    -requires Microsoft.Component.MSBuild `
    -find MSBuild\**\Bin\MSBuild.exe |
    Select-Object -First 1

if (-not $msbuild) {
    throw "Could not find Visual Studio MSBuild."
}

Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------
# Publish function
# ------------------------------------------------------------

Write-Host "Restoring..." -ForegroundColor DarkCyan
& $msbuild $Project /t:Restore /nologo

if ($LASTEXITCODE -ne 0) {
    throw "Restore failed"
}

function Publish-App {
    param (
        [string]$Configuration,
        [string]$Architecture,
        [string]$Runtime
    )

    $configLower = $Configuration.ToLower()
    $outputName = "$AppName-$Architecture-$configLower.exe"
    $tempDir = Join-Path $PublishDir "$Architecture-$configLower"

    Write-Host ""
    
    Write-Host "Publishing $Configuration / $Architecture..." -ForegroundColor Cyan

    & $msbuild $Project `
        /t:Publish `
        "/p:Configuration=$Configuration" `
        "/p:RuntimeIdentifier=$Runtime" `
        "/p:SelfContained=false" `
        "/p:PublishSingleFile=true" `
        "/p:DebugType=None" `
        "/p:DebugSymbols=false" `
        "/p:PublishDir=$tempDir\" `
        /nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for $Configuration / $Architecture"
    }

    # Find the generated EXE
    $exe = Get-ChildItem $tempDir -Filter "*.exe" |
        Select-Object -First 1

    if ($null -eq $exe) {
        throw "Could not find EXE in $tempDir"
    }

    # Move and rename it
    Move-Item $exe.FullName (Join-Path $PublishDir $outputName)

    # Remove temporary publish directory
    Remove-Item $tempDir -Recurse -Force

    Write-Host "  Created $outputName" -ForegroundColor Green
}

# ------------------------------------------------------------
# Build all 4 versions
# ------------------------------------------------------------

Publish-App "Debug"   "x86" "win-x86"
Publish-App "Debug"   "x64" "win-x64"
Publish-App "Release" "x86" "win-x86"
Publish-App "Release" "x64" "win-x64"

# ------------------------------------------------------------
# Done
# ------------------------------------------------------------

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "         PUBLISH COMPLETE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Get-ChildItem $PublishDir -Filter "*.exe" |
    Select-Object Name, @{Name="Size MB"; Expression={[math]::Round($_.Length / 1MB, 2)}} |
    Format-Table -AutoSize