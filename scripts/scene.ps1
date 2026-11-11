# Opens the rendering prototype: the candle-lit room, the editor on the monitor and the Papa-Figo at each
# station. Built in Release, as the game will ship. Keys: F2 next station, F3 measurements on screen,
# F4 quality, F5 he approaches by himself, F6 60 or 30 frames per second, F11 full screen.
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'dotnet.ps1') run --project (Join-Path $root 'src/Candeia.Windows') -c Release -- --scene
exit $LASTEXITCODE
