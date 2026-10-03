<#
    Builds the optional Windows installer (dist/AniVault-<version>-Setup.exe) with Inno Setup.

    Prerequisites:
      * build/publish.ps1 has been run (needs artifacts/publish/AniVault.exe + README.txt)
      * Inno Setup 6 is installed. If ISCC.exe is not found this script tries:
            winget install --id JRSoftware.InnoSetup -e

    Usage (repo root):
        pwsh build/make-installer.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$iss      = Join-Path $repoRoot 'build/installer/AniVault.iss'
$project  = Join-Path $repoRoot 'src/AniVault/AniVault.csproj'
$publishedExe = Join-Path $repoRoot 'artifacts/publish/AniVault.exe'
$publishedReadme = Join-Path $repoRoot 'artifacts/publish/README.txt'
$distDir  = Join-Path $repoRoot 'dist'

if (-not (Test-Path $publishedExe) -or -not (Test-Path $publishedReadme)) {
    throw "artifacts/publish/AniVault.exe or README.txt not found. Run build/publish.ps1 first."
}

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
$version = "$version".Trim()

function Find-Iscc {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-Iscc
if (-not $iscc) {
    Write-Host "Inno Setup not found - installing via winget..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements --disable-interactivity
    $iscc = Find-Iscc
}
if (-not $iscc) { throw "Could not locate ISCC.exe (Inno Setup compiler)." }

Write-Host "AniVault $version  ->  installer" -ForegroundColor Cyan
Write-Host "ISCC: $iscc"

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

& $iscc `
    "/DAppVersion=$version" `
    "/DSourceExe=$publishedExe" `
    "/DSourceReadme=$publishedReadme" `
    "/DOutputDir=$distDir" `
    $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)." }

$setup = Join-Path $distDir "AniVault-$version-Setup.exe"
if (Test-Path $setup) {
    $mb = [Math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host ""
    Write-Host "Installer: $setup ($mb MB)" -ForegroundColor Green

    # Refresh checksums for everything in dist/.
    $sums = Join-Path $distDir 'SHA256SUMS.txt'
    Get-ChildItem $distDir -File | Where-Object { $_.Extension -in '.zip', '.exe' } |
        ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.Name } |
        Set-Content -Encoding ASCII $sums
    Write-Host "Checksums updated: $sums" -ForegroundColor Green
}
