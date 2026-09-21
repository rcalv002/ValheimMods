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
    $version = (Get-Content -LiteralPath (Join-Path $repoPath 'manifest.json') -Raw | ConvertFrom-Json).version_number
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
    $releasePath = Join-Path $repoPath "dist\ValhallaMeter-$version"
    $pluginPath = Join-Path $releasePath 'BepInEx\plugins\ValhallaMeter'
    New-Item -ItemType Directory -Force -Path $pluginPath | Out-Null
    foreach ($name in @('ValhallaMeter.dll', 'ValhallaMeter.Core.dll')) {
        Copy-Item -LiteralPath (Join-Path $repoPath "src\ValhallaMeter\bin\Release\netstandard2.1\$name") -Destination (Join-Path $pluginPath $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $repoPath 'README.md') -Destination $releasePath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'CHANGELOG.md') -Destination $releasePath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'LICENSE') -Destination $releasePath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'manifest.json') -Destination $releasePath -Force
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
