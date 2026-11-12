# Plays night 1 by itself in a 1920x1080 window, above the other windows, for about a minute: saves one
# image of each moment and measures typing, frame rate and processor. Results in test-artifacts/noite/.
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
& $dotnet build (Join-Path $root 'src/Candeia.Windows') -c Release -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$folder = Join-Path $root 'test-artifacts/noite'
New-Item -ItemType Directory -Force $folder | Out-Null
$exe = Join-Path $root 'src/Candeia.Windows/bin/Release/net10.0-windows/Candeia.exe'
Start-Process -FilePath $exe -ArgumentList '--night-images', "`"$folder`"" -Wait
$report = Join-Path $folder 'medicao.md'
$process = Start-Process -FilePath $exe -ArgumentList '--night-bench', "`"$report`"" -PassThru -Wait
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report -Encoding UTF8 }
exit $process.ExitCode
