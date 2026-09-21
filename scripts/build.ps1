param([string]$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
Push-Location $repoPath
try {
    dotnet build 'src\ValhallaMeter\ValhallaMeter.csproj' -c Release "-p:ValheimPath=$ValheimPath" --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project 'tests\ValhallaMeter.Tests' -c Release --no-launch-profile -- $ValheimPath
    if ($LASTEXITCODE -ne 0) { throw 'Validation failed.' }
    dotnet run --project 'tests\ValhallaMeter.OverlayTests' -c Release --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'Overlay validation failed.' }
    $node = Get-Command node -ErrorAction SilentlyContinue
    if ($node) {
        $reportTemplate = Get-Content -LiteralPath (Join-Path $repoPath 'src\ValhallaMeter\report.html') -Raw
        $reportScript = [regex]::Match($reportTemplate, '<script>([\s\S]*)</script>').Groups[1].Value.Replace('__FIGHT_DATA__', '{}').Replace('__REPORT_NOW__', '0')
        $scriptCheck = Join-Path $repoPath 'artifacts\report-script-check.js'
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $scriptCheck) | Out-Null
        Set-Content -LiteralPath $scriptCheck -Value $reportScript -Encoding utf8
        & $node.Source --check $scriptCheck
        if ($LASTEXITCODE -ne 0) { throw 'Offline report JavaScript validation failed.' }
    } else { Write-Warning 'Node.js is unavailable; skipped offline report JavaScript syntax validation.' }
    $version = (Get-Content -LiteralPath (Join-Path $repoPath 'manifest.json') -Raw | ConvertFrom-Json).version_number
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
    $releasePath = Join-Path $repoPath "dist\ValhallaMeter-$version"
    $pluginPath = Join-Path $releasePath 'BepInEx\plugins\ValhallaMeter'
    $resolvedRelease = [System.IO.Path]::GetFullPath($releasePath)
    $resolvedDist = [System.IO.Path]::GetFullPath((Join-Path $repoPath 'dist'))
    if (!$resolvedRelease.StartsWith($resolvedDist + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Release path escaped dist.' }
    if (Test-Path -LiteralPath $resolvedRelease) { Remove-Item -LiteralPath $resolvedRelease -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $pluginPath | Out-Null
    foreach ($name in @('ValhallaMeter.dll', 'ValhallaMeter.Core.dll')) {
        Copy-Item -LiteralPath (Join-Path $repoPath "src\ValhallaMeter\bin\Release\netstandard2.1\$name") -Destination (Join-Path $pluginPath $name) -Force
    }
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $pluginPath 'ValhallaMeter.dll')).Version.ToString(3)
    if ($assemblyVersion -ne $version) { throw "Assembly version $assemblyVersion does not match manifest $version." }
    Copy-Item -LiteralPath (Join-Path $repoPath 'README.md') -Destination $releasePath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'CHANGELOG.md') -Destination $releasePath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'LICENSE') -Destination $releasePath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'manifest.json') -Destination $releasePath -Force
    $checksums = foreach ($file in Get-ChildItem -LiteralPath $releasePath -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($releasePath.Length + 1).Replace('\', '/')
        "{0}  {1}" -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash, $relative
    }
    Set-Content -LiteralPath (Join-Path $releasePath 'SHA256SUMS.txt') -Value $checksums -Encoding ascii
    $zipPath = Join-Path $repoPath "dist\ValhallaMeter-$version.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $releasePath -Recurse -File) {
            $entry = $file.FullName.Substring($releasePath.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry) | Out-Null
        }
    } finally { $zip.Dispose() }
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    Write-Output "Release: $zipPath"
} finally { Pop-Location }
