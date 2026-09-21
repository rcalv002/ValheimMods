param(
    [string]$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [switch]$ReleaseOnly
)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'verify-release.ps1')
$version = (Get-Content -LiteralPath (Join-Path $repoPath 'manifest.json') -Raw | ConvertFrom-Json).version_number
$releaseRoot = Join-Path $repoPath "dist\ValhallaMeter-$version"
Assert-ValhallaRelease -ReleasePath $releaseRoot -ExpectedVersion $version
Write-Output "Verified release contents and versions for Valhalla Meter $version."
if ($ReleaseOnly) { return }

$gamePath = (Resolve-Path -LiteralPath $ValheimPath).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $gamePath 'valheim.exe')) -and !(Test-Path -LiteralPath (Join-Path $gamePath 'valheim_server.exe'))) { throw 'Not a Valheim installation.' }
$releasePlugin = Join-Path $releaseRoot 'BepInEx\plugins\ValhallaMeter'
$installedPlugin = Join-Path $gamePath 'BepInEx\plugins\ValhallaMeter'
foreach ($name in @('ValhallaMeter.Core.dll','ValhallaMeter.dll')) {
    $expected = Join-Path $releasePlugin $name
    $installed = Join-Path $installedPlugin $name
    if (!(Test-Path -LiteralPath $installed -PathType Leaf)) { throw "Installed DLL is missing: $name" }
    if ((Get-FileHash -LiteralPath $expected -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash) {
        throw "Installed DLL differs from release: $name"
    }
}
Write-Output "Verified installed Valhalla Meter $version at $installedPlugin."
