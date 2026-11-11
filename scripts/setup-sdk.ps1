# Installs the exact SDK named in global.json into %LOCALAPPDATA%/Candeia-Development/dotnet,
# without touching the machine-wide SDK. Downloads the official zip and checks its SHA-512.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
$channel = ($version -split '\.')[0..1] -join '.'
$metadata = Invoke-RestMethod "https://builds.dotnet.microsoft.com/dotnet/release-metadata/$channel/releases.json"
$sdk = @($metadata.releases | ForEach-Object { $_.sdks }) | Where-Object version -eq $version | Select-Object -First 1
if (-not $sdk) { throw "SDK $version not found in the official release metadata." }
$download = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
$destination = Join-Path $env:LOCALAPPDATA 'Candeia-Development/dotnet'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$archive = Join-Path $destination "sdk-$version.zip"
Invoke-WebRequest $download.url -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $download.hash) { throw 'SDK checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
& (Join-Path $destination 'dotnet.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'SDK installation failed.' }
