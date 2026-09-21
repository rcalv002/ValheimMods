[CmdletBinding(SupportsShouldProcess)]
param([string]$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$gamePath = (Resolve-Path -LiteralPath $ValheimPath).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $gamePath 'valheim.exe')) -and !(Test-Path -LiteralPath (Join-Path $gamePath 'valheim_server.exe'))) { throw 'Not a Valheim installation.' }
$version = (Get-Content -LiteralPath (Join-Path $repoPath 'manifest.json') -Raw | ConvertFrom-Json).version_number
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
$sourcePath = Join-Path $repoPath "dist\ValhallaMeter-$version\BepInEx\plugins\ValhallaMeter"
$sourceVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $sourcePath 'ValhallaMeter.dll')).Version.ToString(3)
if ($sourceVersion -ne $version) { throw "Release assembly version $sourceVersion does not match manifest $version." }
$targetPath = Join-Path $gamePath 'BepInEx\plugins\ValhallaMeter'
$backupPath = Join-Path $repoPath ('artifacts\backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
$items = foreach ($name in @('ValhallaMeter.Core.dll','ValhallaMeter.dll')) {
    $source = Join-Path $sourcePath $name
    $target = [System.IO.Path]::GetFullPath((Join-Path $targetPath $name))
    if (!$target.StartsWith($gamePath + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Destination escaped game directory.' }
    if (!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $target)) { throw "Missing source or installed DLL: $name. Use the install script for a new installation." }
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        [pscustomobject]@{Source=$source;Target=$target;Name=$name}
    }
}
foreach ($item in $items) {
    if ($PSCmdlet.ShouldProcess($item.Target, 'Back up and atomically replace mod DLL')) {
        New-Item -ItemType Directory -Force -Path $backupPath | Out-Null
        Copy-Item -LiteralPath $item.Target -Destination (Join-Path $backupPath $item.Name)
        $staged = $item.Target + '.update-' + [guid]::NewGuid().ToString('N')
        Copy-Item -LiteralPath $item.Source -Destination $staged
        try {
            # Replacing the directory entry preserves an already-open file mapping.
            # If Windows denies replacement, never fall back to overwriting in place.
            $replacementBackup = Join-Path $backupPath ($item.Name + '.replaced')
            [System.IO.File]::Replace($staged, $item.Target, $replacementBackup)
        } catch {
            Remove-Item -LiteralPath $staged -ErrorAction SilentlyContinue
            throw "Windows could not replace $($item.Name). Previous DLLs are backed up at $backupPath. No running process was stopped. $($_.Exception.Message)"
        }
        if ((Get-FileHash -LiteralPath $item.Source).Hash -ne (Get-FileHash -LiteralPath $item.Target).Hash) { throw 'Installed hash mismatch.' }
    }
}
if (!$WhatIfPreference) {
    Write-Output "Verified Valhalla Meter $version. Restart Valheim to load it. F7 shows/hides; F8 enables clicking and dragging."
    if ($items) { Write-Output "Previous DLL backup: $backupPath" }
}
