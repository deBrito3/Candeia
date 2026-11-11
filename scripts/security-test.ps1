# Attacks the isolated runner with code the checker would refuse and prints a report:
# which layer stopped each attack (the checker, the operating-system isolation, or only the checker).
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'tests/Candeia.Tests') -c Debug -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project (Join-Path $root 'tests/Candeia.Tests') -c Debug --no-build -- --security-test
exit $LASTEXITCODE
