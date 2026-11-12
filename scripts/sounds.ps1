# Writes every sound of the game, and a 40-second night mixed by the real sound director, as WAV files in
# test-artifacts/som, to listen to without playing.
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'tests/Candeia.Tests') -c Debug -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$folder = Join-Path $root 'test-artifacts/som'
& $dotnet run --project (Join-Path $root 'tests/Candeia.Tests') -c Debug --no-build -- --sounds $folder
exit $LASTEXITCODE
