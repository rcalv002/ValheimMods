# Run after building both releases. Uses a temporary fake game folder; no game is launched.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$artifactRoot = Join-Path $repo 'artifacts'
$fixture = Join-Path $artifactRoot ('install-test-' + [guid]::NewGuid().ToString('N'))
$backups = [System.Collections.Generic.List[string]]::new()
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $fixture 'BepInEx\core') | Out-Null
    New-Item -ItemType File -Path (Join-Path $fixture 'valheim.exe') | Out-Null
    New-Item -ItemType File -Path (Join-Path $fixture 'BepInEx\core\BepInEx.dll') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $fixture 'BepInEx\config') | Out-Null
    $config = Join-Path $fixture 'BepInEx\config\keep.cfg'
    [System.IO.File]::WriteAllText($config, 'keep existing settings')
    foreach ($plugin in @('HeadingCompass', 'TargetCall')) {
        $slug = $plugin.ToLowerInvariant()
        $installer = Join-Path $repo "scripts\install-$slug.ps1"
        $manifest = Get-Content -LiteralPath (Join-Path $repo "src\$plugin\manifest.json") -Raw | ConvertFrom-Json
        $source = Join-Path $repo "dist\$plugin-$($manifest.version_number)\BepInEx\plugins\$plugin\$plugin.dll"
        $target = Join-Path $fixture "BepInEx\plugins\$plugin\$plugin.dll"
        & $installer -ValheimPath $fixture -WhatIf
        if (Test-Path -LiteralPath $target) { throw 'WhatIf wrote a DLL.' }
        & $installer -ValheimPath $fixture
        $expected = (Get-FileHash -LiteralPath $source).Hash
        if ((Get-FileHash -LiteralPath $target).Hash -ne $expected) { throw 'Fresh installation differs from release.' }
        & $installer -ValheimPath $fixture
        [System.IO.File]::WriteAllText($target, 'old plugin bytes')
        $old = (Get-FileHash -LiteralPath $target).Hash
        $output = @(& $installer -ValheimPath $fixture)
        $backup = ($output | Where-Object { $_ -like 'Backup: *' } | Select-Object -First 1).Substring(8)
        if (!$backup.StartsWith((Join-Path $artifactRoot 'backups') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unexpected backup destination.'
        }
        $backups.Add($backup); $backups.Add($backup + '.replaced')
        if ((Get-FileHash -LiteralPath $target).Hash -ne $expected -or
            (Get-FileHash -LiteralPath $backup).Hash -ne $old -or
            (Get-FileHash -LiteralPath ($backup + '.replaced')).Hash -ne $old) { throw 'Upgrade/backup mismatch.' }
        if ([System.IO.File]::ReadAllText($config) -ne 'keep existing settings') { throw 'Configuration was changed.' }
        if (@(Get-ChildItem -LiteralPath (Split-Path $target) -Filter '*.update-*').Count -ne 0) { throw 'Staged DLL was left behind.' }
        Write-Output "PASS $plugin preview, fresh install, repeat install, atomic upgrade, backup and configuration preservation"
    }
} finally {
    foreach ($backup in $backups) { if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup } }
    $resolved = [System.IO.Path]::GetFullPath($fixture)
    if ($resolved.StartsWith($artifactRoot + '\', [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
