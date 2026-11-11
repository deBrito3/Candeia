# Runs dotnet with the isolated development SDK when it exists (see setup-sdk.ps1),
# otherwise with the SDK installed on the machine. global.json pins the supported SDK.
$localSdk = Join-Path $env:LOCALAPPDATA 'Candeia-Development/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $localSdk) { & $localSdk @args }
else { & dotnet @args }
exit $LASTEXITCODE
