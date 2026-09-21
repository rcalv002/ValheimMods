function Assert-ValhallaRelease {
    param(
        [Parameter(Mandatory)][string]$ReleasePath,
        [Parameter(Mandatory)][string]$ExpectedVersion
    )

    if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid expected release version.' }
    if (!(Test-Path -LiteralPath $ReleasePath -PathType Container)) { throw "Release directory is missing: $ReleasePath" }
    $root = (Resolve-Path -LiteralPath $ReleasePath).Path.TrimEnd('\')
    $checksumPath = Join-Path $root 'SHA256SUMS.txt'
    if (!(Test-Path -LiteralPath $checksumPath -PathType Leaf)) { throw 'Release checksum file is missing.' }

    $entries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($line in Get-Content -LiteralPath $checksumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$') { throw "Malformed checksum line: $line" }
        $expectedHash = $Matches[1].ToUpperInvariant()
        $relative = $Matches[2].Replace('/', '\')
        if ([System.IO.Path]::IsPathRooted($relative)) { throw "Rooted checksum path: $relative" }
        $filePath = [System.IO.Path]::GetFullPath((Join-Path $root $relative))
        if (!$filePath.StartsWith($root + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw "Checksum path escaped release: $relative" }
        $normalized = $filePath.Substring($root.Length + 1).Replace('\', '/')
        if ($normalized -eq 'SHA256SUMS.txt' -or !$entries.Add($normalized)) { throw "Duplicate or recursive checksum entry: $normalized" }
        if (!(Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Checksummed file is missing: $normalized" }
        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) { throw "Checksum mismatch: $normalized" }
    }

    $required = @('manifest.json','README.md','CHANGELOG.md','LICENSE',
        'BepInEx/plugins/ValhallaMeter/ValhallaMeter.dll',
        'BepInEx/plugins/ValhallaMeter/ValhallaMeter.Core.dll')
    foreach ($relative in $required) {
        if (!$entries.Contains($relative)) { throw "Required release entry is missing from checksums: $relative" }
    }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        $relative = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
        if ($relative -ne 'SHA256SUMS.txt' -and !$entries.Contains($relative)) { throw "Unchecksummed release file: $relative" }
    }

    $manifestVersion = (Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json).version_number
    if ($manifestVersion -ne $ExpectedVersion) { throw "Release manifest version $manifestVersion does not match $ExpectedVersion." }
    $assemblyPath = Join-Path $root 'BepInEx\plugins\ValhallaMeter\ValhallaMeter.dll'
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString(3)
    if ($assemblyVersion -ne $ExpectedVersion) { throw "Release assembly version $assemblyVersion does not match $ExpectedVersion." }
}
