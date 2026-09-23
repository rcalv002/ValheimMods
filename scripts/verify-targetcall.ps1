param([string]$ReleasePath = '')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$version = (Get-Content -LiteralPath (Join-Path $repoPath 'src\TargetCall\manifest.json') -Raw | ConvertFrom-Json).version_number
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Target Call version.' }
if (!$ReleasePath) { $ReleasePath = Join-Path $repoPath "dist\TargetCall-$version" }
$root = (Resolve-Path -LiteralPath $ReleasePath).Path.TrimEnd('\')
$expected = @('BepInEx/plugins/TargetCall/TargetCall.dll', 'LICENSE', 'manifest.json', 'README.md')
$checksumPath = Join-Path $root 'SHA256SUMS.txt'
$lines = @(Get-Content -LiteralPath $checksumPath)
if ($lines.Count -ne $expected.Count) { throw 'Unexpected Target Call checksum count.' }
$seen = @{}
foreach ($line in $lines) {
    if ($line -cnotmatch '^([A-F0-9]{64})  (.+)$') { throw 'Malformed Target Call checksum line.' }
    $hash = $Matches[1]
    $relative = $Matches[2]
    if ($relative -cnotin $expected -or $seen.ContainsKey($relative)) { throw "Unexpected or duplicate release file: $relative" }
    $seen[$relative] = $true
    $path = Join-Path $root ($relative.Replace('/', '\'))
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $hash) {
        throw "Target Call release hash mismatch: $relative"
    }
}
$actual = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
if (@($actual | Where-Object { $_ -ne 'SHA256SUMS.txt' -and $_ -cnotin $expected }).Count -gt 0 -or
    @($expected | Where-Object { $_ -cnotin $actual }).Count -gt 0) { throw 'Missing or unlisted Target Call release file.' }
$dll = Join-Path $root 'BepInEx\plugins\TargetCall\TargetCall.dll'
if ([System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $version) {
    throw 'Target Call assembly version mismatch.'
}
if ((Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json).version_number -ne $version) {
    throw 'Target Call manifest version mismatch.'
}
Write-Output "Verified Target Call $version release and SHA-256 checksums."
