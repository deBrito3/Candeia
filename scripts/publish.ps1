# Builds the test version: dist/Candeia-<version>-teste.zip, with
#   Candeia.exe       the game as one file, with .NET inside (nothing to install), x64
#   content/          the nights and the notebook, beside it, so they can change without a new build
#   licenses/         the third-party licenses, and LICENSE.txt with Candeia's own terms
#   COMO-TESTAR.txt   the guide for testers, in Portuguese and English
# The build maps source paths (ContinuousIntegrationBuild), so the stack traces in a crash report never
# carry this computer's folders; the script checks the executable for them before zipping.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup |
    ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1
$name = "Candeia-$version-teste"
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist $name
$zip = Join-Path $dist "$name.zip"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }

& $dotnet publish src/Candeia.Windows -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:ContinuousIntegrationBuild=true -p:DebugType=embedded -o $stage
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The runtime inside the executable must be the one whose licenses are in licenses/.
$deps = Get-Content -Raw -LiteralPath (Join-Path $root 'src/Candeia.Windows/obj/Release/net10.0-windows/win-x64/Candeia.deps.json') | ConvertFrom-Json
$runtime = $null
foreach ($target in $deps.targets.PSObject.Properties) {
    foreach ($entry in $target.Value.PSObject.Properties) {
        if ($entry.Name -like 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/*') { $runtime = $entry.Name.Split('/')[1] }
    }
}
if (-not $runtime) {
    Write-Error "The .NET runtime version is not in Candeia.deps.json; the publish did not go as expected."
    exit 1
}
$listed = Select-String -LiteralPath (Join-Path $root 'licenses/README.md') -Pattern "\| \.NET runtime .*\| $([regex]::Escape($runtime)) \|" -Quiet
if (-not $listed) {
    Write-Error "The game was built with .NET runtime $runtime, but licenses/README.md lists another version. Copy LICENSE and THIRD-PARTY-NOTICES from the $runtime runtime packages into licenses/ and update the table."
    exit 1
}

# Only the executable and the content: no debug files or leftovers.
Get-ChildItem -LiteralPath $stage -File | Where-Object { $_.Name -ne 'Candeia.exe' } | Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination (Join-Path $stage 'licenses') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'LICENSE.txt') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs/COMO-TESTAR.txt') -Destination $stage

# No folder of this computer inside the executable (they would reach the testers in a crash report).
$exe = Join-Path $stage 'Candeia.exe'
$bytes = [System.IO.File]::ReadAllBytes($exe)
$text = [System.Text.Encoding]::ASCII.GetString($bytes) + [System.Text.Encoding]::Unicode.GetString($bytes)
foreach ($private in @($env:USERNAME, $root) | Where-Object { $_ -and $_.Length -ge 3 }) {
    if ($text.IndexOf($private, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Write-Error "Candeia.exe contains a path of this computer; the build did not map the source paths."
        exit 1
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)
$size = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
$exeSize = [math]::Round((Get-Item -LiteralPath $exe).Length / 1MB, 1)
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Write-Output "Pronto: $zip ($size MB; Candeia.exe $exeSize MB; .NET $runtime)"
Write-Output "SHA-256: $hash"
