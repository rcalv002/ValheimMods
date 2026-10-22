param([string]$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $repoPath 'src\HeadingCompass\manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Heading Compass release version.' }
$releasePath = Join-Path $repoPath "dist\HeadingCompass-$version"
$resolvedRelease = [System.IO.Path]::GetFullPath($releasePath)
$resolvedDist = [System.IO.Path]::GetFullPath((Join-Path $repoPath 'dist'))
if (!$resolvedRelease.StartsWith($resolvedDist + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Release path escaped dist.' }
Push-Location $repoPath
try {
    dotnet build 'src\HeadingCompass\HeadingCompass.csproj' -c Release "-p:ValheimPath=$ValheimPath" -p:UseSharedCompilation=false --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) { throw 'Heading Compass build failed.' }
    dotnet run --project 'tests\HeadingCompass.Tests' -c Release --no-launch-profile -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Heading Compass checks failed.' }
    if (Test-Path -LiteralPath $resolvedRelease) { Remove-Item -LiteralPath $resolvedRelease -Recurse -Force }
    $pluginPath = Join-Path $releasePath 'BepInEx\plugins\HeadingCompass'
    New-Item -ItemType Directory -Force -Path $pluginPath | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoPath 'src\HeadingCompass\bin\Release\netstandard2.1\HeadingCompass.dll') -Destination $pluginPath
    Copy-Item -LiteralPath (Join-Path $repoPath 'src\HeadingCompass\manifest.json') -Destination $releasePath
    Copy-Item -LiteralPath (Join-Path $repoPath 'src\HeadingCompass\README.md') -Destination $releasePath
    Copy-Item -LiteralPath (Join-Path $repoPath 'LICENSE') -Destination $releasePath
    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $pluginPath 'HeadingCompass.dll')).Version.ToString(3)
    if ($assemblyVersion -ne $version) { throw "Assembly version $assemblyVersion does not match manifest $version." }
    $checksums = foreach ($file in Get-ChildItem -LiteralPath $releasePath -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($releasePath.Length + 1).Replace('\', '/')
        "{0}  {1}" -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash, $relative
    }
    Set-Content -LiteralPath (Join-Path $releasePath 'SHA256SUMS.txt') -Value $checksums -Encoding ascii
    & (Join-Path $PSScriptRoot 'verify-headingcompass.ps1') -ReleasePath $releasePath
    $zipPath = Join-Path $repoPath "dist\HeadingCompass-$version.zip"
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
