# Runs dotnet with the isolated development SDK when it exists (see setup-sdk.ps1), otherwise with the
# SDK installed on the machine. It always runs from the repository root: dotnet picks the SDK from the
# global.json of the current folder, so starting from another project (with its own global.json) would
# ask for that project's SDK instead of Candeia's.
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $env:LOCALAPPDATA 'Candeia-Development/dotnet/dotnet.exe'
Push-Location -LiteralPath $root
try {
    if (Test-Path -LiteralPath $localSdk) { & $localSdk @args }
    else { & dotnet @args }
    $code = $LASTEXITCODE
}
finally { Pop-Location }
exit $code
