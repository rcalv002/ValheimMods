param([string]$Version = '5.4.2350')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$dependencyPath = Join-Path $repoPath '.deps'
New-Item -ItemType Directory -Force -Path $dependencyPath | Out-Null
$archivePath = Join-Path $dependencyPath 'bepinex.zip'
Invoke-WebRequest -Uri "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/$Version/" -OutFile $archivePath
if ($Version -eq '5.4.2350' -and (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne '37A91C000B4E88F2ED7A4BD7D812239852D2E36CBF0FF0A9F5FAACFBA46B105F') {
    throw 'Downloaded loader archive differs from the verified build dependency.'
}
Expand-Archive -LiteralPath $archivePath -DestinationPath (Join-Path $dependencyPath 'bepinex') -Force
Write-Output "Prepared Valheim BepInEx pack $Version in .deps. No game files changed."
