# Checks C# files as a night would and prints the explained errors.
#   ./scripts/check.ps1                       every file in samples/csharp
#   ./scripts/check.ps1 my-file.cs --en       one file, messages in English (--linq allows LINQ)
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
$files = @($args | Where-Object { -not $_.StartsWith('--') })
$flags = @($args | Where-Object { $_.StartsWith('--') })
if ($files.Count -eq 0) { $files = Get-ChildItem (Join-Path $root 'samples/csharp') -Filter *.cs | Sort-Object Name | ForEach-Object FullName }
& $dotnet build (Join-Path $root 'tests/Candeia.Tests') -c Debug -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($file in $files) {
    & $dotnet run --project (Join-Path $root 'tests/Candeia.Tests') -c Debug --no-build -- --check $file @flags
    ''
}
