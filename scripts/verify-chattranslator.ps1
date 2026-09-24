param([string]$ReleasePath = '')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$version = (Get-Content -LiteralPath (Join-Path $repoPath 'src\ChatTranslator\manifest.json') -Raw | ConvertFrom-Json).version_number
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Chat Translator version.' }
if (!$ReleasePath) { $ReleasePath = Join-Path $repoPath "dist\ChatTranslator-$version" }
$root = (Resolve-Path -LiteralPath $ReleasePath).Path.TrimEnd('\')
$expected = @('BepInEx/plugins/ChatTranslator/ChatTranslator.dll', 'LICENSE', 'manifest.json', 'README.md')
$checksumPath = Join-Path $root 'SHA256SUMS.txt'
$lines = @(Get-Content -LiteralPath $checksumPath)
if ($lines.Count -ne $expected.Count) { throw 'Unexpected Chat Translator checksum count.' }
$seen = @{}
foreach ($line in $lines) {
    if ($line -cnotmatch '^([A-F0-9]{64})  (.+)$') { throw 'Malformed Chat Translator checksum line.' }
    $hash = $Matches[1]
    $relative = $Matches[2]
    if ($relative -cnotin $expected -or $seen.ContainsKey($relative)) { throw "Unexpected or duplicate release file: $relative" }
    $seen[$relative] = $true
    $path = Join-Path $root ($relative.Replace('/', '\'))
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $hash) {
        throw "Chat Translator release hash mismatch: $relative"
    }
}
$actual = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
if (@($actual | Where-Object { $_ -ne 'SHA256SUMS.txt' -and $_ -cnotin $expected }).Count -gt 0 -or
    @($expected | Where-Object { $_ -cnotin $actual }).Count -gt 0) { throw 'Missing or unlisted Chat Translator release file.' }
$dll = Join-Path $root 'BepInEx\plugins\ChatTranslator\ChatTranslator.dll'
if ([System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $version) {
    throw 'Chat Translator assembly version mismatch.'
}
Write-Output "Verified Chat Translator $version release and SHA-256 checksums."
