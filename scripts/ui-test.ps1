# Checks the night screen in real windows: editor colours, indentation, undo, completion, the LEIA-ME and
# the night's keys. Opens the game for a few seconds, above the other windows.
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'src/Candeia.Windows') -c Release -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$folder = Join-Path $root 'test-artifacts/noite'
New-Item -ItemType Directory -Force $folder | Out-Null
$report = Join-Path $folder 'ui.txt'
$exe = Join-Path $root 'src/Candeia.Windows/bin/Release/net10.0-windows/Candeia.exe'
$process = Start-Process -FilePath $exe -ArgumentList '--ui-test', "`"$report`"" -PassThru -Wait
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Encoding UTF8 }
exit $process.ExitCode
