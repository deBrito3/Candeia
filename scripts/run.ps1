# Opens the game. Extra arguments go to the game, for example: ./scripts/run.ps1 --shot test-artifacts/boot.png
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'dotnet.ps1') run --project (Join-Path $root 'src/Candeia.Windows') -c Debug -- @args
exit $LASTEXITCODE
