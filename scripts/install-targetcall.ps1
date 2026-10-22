[CmdletBinding(SupportsShouldProcess)]
param([string]$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$version = (Get-Content -LiteralPath (Join-Path $repoPath 'src\TargetCall\manifest.json') -Raw | ConvertFrom-Json).version_number
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Target Call version.' }
$release = Join-Path $repoPath "dist\TargetCall-$version"
& (Join-Path $PSScriptRoot 'verify-targetcall.ps1') -ReleasePath $release
$game = (Resolve-Path -LiteralPath $ValheimPath).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Join-Path $game 'valheim.exe'))) { throw 'Valheim client installation missing.' }
if (!(Test-Path -LiteralPath (Join-Path $game 'BepInEx\core\BepInEx.dll'))) { throw 'Install BepInExPack Valheim first.' }
$source = Join-Path $release 'BepInEx\plugins\TargetCall\TargetCall.dll'
$target = [System.IO.Path]::GetFullPath((Join-Path $game 'BepInEx\plugins\TargetCall\TargetCall.dll'))
if (!$target.StartsWith($game + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Target escaped game directory.' }
$expectedHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $expectedHash) {
    Write-Output "Target Call $version is already installed."
    return
}
if (!$PSCmdlet.ShouldProcess($target, "Install Target Call $version for the next game launch")) { return }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
$backup = $null
if (Test-Path -LiteralPath $target) {
    $backup = Join-Path $repoPath ('artifacts\backups\targetcall-' + [guid]::NewGuid().ToString('N') + '.dll')
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
    Copy-Item -LiteralPath $target -Destination $backup
    Write-Output "Backup: $backup"
}
$staged = $target + '.update-' + [guid]::NewGuid().ToString('N')
try {
    Copy-Item -LiteralPath $source -Destination $staged
    if ((Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Staged hash mismatch.' }
    if (Test-Path -LiteralPath $target) { [System.IO.File]::Replace($staged, $target, ($backup + '.replaced')) }
    else { Move-Item -LiteralPath $staged -Destination $target }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Installed hash mismatch.' }
} finally {
    if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged }
}
Write-Output "Installed Target Call $version. Restart Valheim when convenient; this script does not stop it."
