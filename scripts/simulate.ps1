# Plays scripted nights through the real clock and creature rules and prints each timeline.
# The same text is saved in test-artifacts/simulacoes.md.
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'tests/Candeia.Tests') -c Debug -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
New-Item -ItemType Directory -Force (Join-Path $root 'test-artifacts') | Out-Null
$report = Join-Path $root 'test-artifacts/simulacoes.md'
& $dotnet run --project (Join-Path $root 'tests/Candeia.Tests') -c Debug --no-build -- --simulate | Tee-Object -FilePath $report
exit $LASTEXITCODE
