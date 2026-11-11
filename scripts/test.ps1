# Builds the whole solution and runs the command-line test suites.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'Candeia.slnx') -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project (Join-Path $root 'tests/Candeia.Tests') -c Debug --no-build -- --self-test
exit $LASTEXITCODE
