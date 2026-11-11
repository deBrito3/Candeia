# Checks every night in content/: the files and their format, the starter code (which must not win)
# and the reference solution in tests/Candeia.Tests/Solutions (which must light every lamp).
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'tests/Candeia.Tests') -c Debug -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project (Join-Path $root 'tests/Candeia.Tests') -c Debug --no-build -- --content-test
exit $LASTEXITCODE
