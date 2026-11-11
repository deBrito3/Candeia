# Measures the rendering prototype in a 1920x1080 window, above the other windows, for about 2 minutes,
# and writes test-artifacts/desenho/relatorio.md with one image per station beside it.
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'src/Candeia.Windows') -c Release -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$folder = Join-Path $root 'test-artifacts/desenho'
New-Item -ItemType Directory -Force $folder | Out-Null
$report = Join-Path $folder 'relatorio.md'
$exe = Join-Path $root 'src/Candeia.Windows/bin/Release/net10.0-windows/Candeia.exe'
$process = Start-Process -FilePath $exe -ArgumentList '--scene-bench', "`"$report`"" -PassThru -Wait
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Encoding UTF8 }
exit $process.ExitCode
