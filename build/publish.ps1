<#
    Publishes AniVault as a portable, self-contained, single-file Windows x64 build and
    packages it as a ZIP under dist/.

    Usage (from the repo root, Windows PowerShell or PowerShell 7):
        pwsh build/publish.ps1
        pwsh build/publish.ps1 -Configuration Release

    Output:
        dist/AniVault-<version>-win-x64-portable/AniVault.exe   (staged folder)
        dist/AniVault-<version>-win-x64-portable.zip            (shippable ZIP)
        dist/SHA256SUMS.txt
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'src/AniVault/AniVault.csproj'
$distDir  = Join-Path $repoRoot 'dist'
$stagingRoot = Join-Path $repoRoot 'artifacts/publish'

function Get-ProjectVersion {
    $xml = [xml](Get-Content $project)
    $v = ($xml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
    if (-not $v) { $v = '0.0.0' }
    return "$v".Trim()
}

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) { $dotnet = $dotnetCmd.Source } else { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

$version = Get-ProjectVersion
$name = "AniVault-$version-win-x64-portable"
Write-Host "AniVault $version  ($Configuration)  ->  $name" -ForegroundColor Cyan

# Clean previous output for this run.
if (Test-Path $stagingRoot) { Remove-Item $stagingRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

& $dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $stagingRoot
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

# Single-file publish should leave essentially just the exe (+ maybe a .pdb we suppressed).
$exe = Join-Path $stagingRoot 'AniVault.exe'
if (-not (Test-Path $exe)) { throw "Expected $exe was not produced." }

$outerDir = Join-Path $distDir $name
$portableDir = Join-Path $outerDir 'AniVault'
if (Test-Path $outerDir) { Remove-Item $outerDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null

# One README for both distributions (make-installer.ps1 picks it up from artifacts/publish).
$readme = Join-Path $stagingRoot 'README.txt'
@"
AniVault $version
==================================

Getting started
  * Portable ZIP: extract this folder anywhere (a normal folder, a USB drive, ...) and
    double-click AniVault.exe.
  * Installer: AniVault.exe is already in place - start it from the Start menu.

On first run, choose where your library data (database, artwork, backups) should live.
Pick a folder you can write to, e.g. D:\AniVaultData.

No .NET runtime is required.

Moving to another PC
  Copy your data folder across, put AniVault on the new PC, and on first run point it at
  the copied data folder - or use Settings > Backup & data > Restore from backup.

Removing AniVault
  There is no uninstaller and nothing is installed outside this folder. Delete this folder
  (plus the Start-menu / desktop shortcut if you used the installer) and the app is gone.
  Your library data folder is separate and is never deleted for you - remove it yourself
  only if you really want to erase your library.
"@ | Set-Content -Encoding UTF8 $readme

Copy-Item $exe (Join-Path $portableDir 'AniVault.exe')
Copy-Item $readme (Join-Path $portableDir 'README.txt')

$zipPath = Join-Path $distDir "$name.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $portableDir -DestinationPath $zipPath -CompressionLevel Optimal

# Checksums for the shippable files at the top of dist/ (ZIP now, installer later).
$sums = Join-Path $distDir 'SHA256SUMS.txt'
Get-ChildItem $distDir -File | Where-Object { $_.Extension -in '.zip', '.exe' } |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.Name } |
    Set-Content -Encoding ASCII $sums

$exeInfo = Get-Item (Join-Path $portableDir 'AniVault.exe')
Write-Host ""
Write-Host "AniVault.exe : $([Math]::Round($exeInfo.Length / 1MB, 1)) MB" -ForegroundColor Green
Write-Host "ZIP          : $zipPath" -ForegroundColor Green
Write-Host "Checksums    : $sums" -ForegroundColor Green
