# Opens the game, built in Release as it will ship. Extra arguments go to the game, for example:
# ./scripts/run.ps1 --night act1-02-system-battery, or ./scripts/run.ps1 --shot test-artifacts/boot.png
# The image path is relative to the folder the command is started from.
$root = Split-Path $PSScriptRoot -Parent
$gameArgs = @($args)
for ($i = 0; $i -lt $gameArgs.Count - 1; $i++) {
    if ($gameArgs[$i] -eq '--shot') { $gameArgs[$i + 1] = [IO.Path]::GetFullPath([IO.Path]::Combine((Get-Location).Path, $gameArgs[$i + 1])) }
}
& (Join-Path $PSScriptRoot 'dotnet.ps1') run --project (Join-Path $root 'src/Candeia.Windows') -c Release -- @gameArgs
exit $LASTEXITCODE
