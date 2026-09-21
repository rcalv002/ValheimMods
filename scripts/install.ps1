[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [switch]$InstallLoader
)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$gamePath = (Resolve-Path -LiteralPath $ValheimPath).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $gamePath 'valheim.exe')) -and !(Test-Path -LiteralPath (Join-Path $gamePath 'valheim_server.exe'))) { throw 'Target is not a Valheim installation.' }
$version = (Get-Content -LiteralPath (Join-Path $repoPath 'manifest.json') -Raw | ConvertFrom-Json).version_number
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
$releasePath = Join-Path $repoPath "dist\ValhallaMeter-$version\BepInEx\plugins\ValhallaMeter"
$loaderPath = Join-Path $repoPath '.deps\bepinex\BepInExPack_Valheim'
foreach ($name in @('ValhallaMeter.dll','ValhallaMeter.Core.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $releasePath $name))) { throw 'Build the release first with scripts/build.ps1.' }
}
$corePath = Join-Path $gamePath 'BepInEx\core\BepInEx.dll'
if (!(Test-Path -LiteralPath $corePath) -and !$InstallLoader) { throw 'BepInEx is missing. Use -InstallLoader for a clean manual installation, or install BepInEx with your mod manager.' }
$copies = [System.Collections.Generic.List[object]]::new()
if ($InstallLoader -and !(Test-Path -LiteralPath $corePath)) {
    if (!(Test-Path -LiteralPath (Join-Path $loaderPath 'winhttp.dll'))) { throw 'Run scripts/prepare.ps1 first.' }
    foreach ($file in Get-ChildItem -LiteralPath $loaderPath -Recurse -File) {
        $relative = $file.FullName.Substring($loaderPath.Length + 1)
        # The Windows install needs no Unix launcher or native library files.
        if ($relative -like 'doorstop_libs\*' -or $relative -like '*.sh' -or $relative -eq 'changelog.txt') { continue }
        $target = Join-Path $gamePath $relative
        if (Test-Path -LiteralPath $target) { throw "Existing loader file would be overwritten: $target. Use your existing mod manager or resolve that installation first." }
        $copies.Add([pscustomobject]@{ Source=$file.FullName; Target=$target })
    }
}
foreach ($file in Get-ChildItem -LiteralPath $releasePath -File) {
    $target = Join-Path $gamePath "BepInEx\plugins\ValhallaMeter\$($file.Name)"
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash) { continue }
        throw "An existing version is installed at $target. Back it up before replacing it."
    }
    $copies.Add([pscustomobject]@{ Source=$file.FullName; Target=$target })
}
if ($copies.Count -eq 0) { Write-Output 'This build is already installed.'; return }
# Every destination is derived from this verified game root; no saves or managed game assemblies are modified.
foreach ($copy in $copies) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($copy.Target)
    if (!$resolvedTarget.StartsWith($gamePath + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Destination escaped the game directory.' }
    if ($PSCmdlet.ShouldProcess($resolvedTarget, 'Install new mod file')) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedTarget) | Out-Null
        Copy-Item -LiteralPath $copy.Source -Destination $resolvedTarget
    }
}
if (!$WhatIfPreference) {
    $receiptPath = Join-Path $repoPath 'artifacts\installation-receipt.json'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $receiptPath) | Out-Null
    $copies | ForEach-Object { [pscustomobject]@{ Path=$_.Target; SHA256=(Get-FileHash -LiteralPath $_.Target).Hash } } | ConvertTo-Json | Set-Content -LiteralPath $receiptPath
    Write-Output 'Installed for the next game launch. Restart Valheim when convenient; this script does not stop it.'
    Write-Output "Receipt: $receiptPath"
}
