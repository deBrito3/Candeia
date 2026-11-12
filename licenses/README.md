# Third-party components / Componentes de terceiros

When a component ships with the game, its license texts go in this directory and a row goes in the table below. Candeia's own terms are in `LICENSE.txt` at the root of the repository and of the test build.

| Component | Version | Source | License files | Ships from |
| --- | --- | --- | --- | --- |
| Microsoft.CodeAnalysis.CSharp and Microsoft.CodeAnalysis.Common (Roslyn) | 5.9.0 | https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/5.9.0 | roslyn/LICENSE.txt; roslyn/ThirdPartyNotices.rtf | Delivery 1 |
| .NET reference assemblies embedded in Candeia.Core (System.Runtime, System.Collections, System.Console, System.Linq) | from the .NET SDK targeting pack | https://dotnet.microsoft.com/ | dotnet/LICENSE.txt; dotnet/THIRD-PARTY-NOTICES.txt | Delivery 1 |
| .NET runtime (Microsoft.NETCore.App, self-contained in Candeia.exe) | 10.0.11 | https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.win-x64/10.0.11 | dotnet/LICENSE.txt; dotnet/THIRD-PARTY-NOTICES.txt | Delivery 10B |
| Windows Desktop runtime (Microsoft.WindowsDesktop.App: Windows Forms, System.Drawing) | 10.0.11 | https://www.nuget.org/packages/Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.11 | windowsdesktop/LICENSE.txt | Delivery 10B |

The game uses Windows-provided fonts; font files are not redistributed. Artwork and sounds are drawn and synthesized by the game's own code.

When the .NET SDK moves to a new runtime version, `scripts/publish.ps1` stops and asks for these files to be updated from the new runtime packages.
