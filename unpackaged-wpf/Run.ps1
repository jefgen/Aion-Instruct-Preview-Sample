# Run.ps1 -- build and run the unpackaged WPF chat sample.
#
# Prereqs (same model dependency as the packaged AionInstructPreview.Chat):
#   - The Aion Instruct Preview SDK framework MSIX is installed
#     (Microsoft.AionInstructPreview.Framework.1.0). See the repo root README
#     for how to install it.
#   - Windows App Runtime 2 and Windows App Runtime 1.8 are installed
#     (winget install --id Microsoft.WindowsAppRuntime.2.0
#      winget install --id Microsoft.WindowsAppRuntime.1.8).
#   - .NET SDK 9 or later.
#
# Unlike the packaged sample, nothing is copied locally: the winmd comes from
# the SDK NuGet at build time, and the model files + native DLLs are consumed
# in place from the installed framework package at runtime.

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot

Write-Host "Checking for the installed Aion Instruct Preview framework package..."
$pkg = Get-AppxPackage "Microsoft.AionInstructPreview.Framework*" |
    Sort-Object Version -Descending |
    Select-Object -First 1

if ($null -eq $pkg) {
    Write-Error "Aion Instruct Preview framework package not installed. Install the framework MSIX first (see the repo root README), then re-run."
    exit 1
}

Write-Host ("Using framework {0} {1} ({2})." -f $pkg.Name, $pkg.Version, $pkg.Architecture)

# The model stack needs both Windows App Runtimes at run time.
foreach ($war in @(
    @{ Name = 'Microsoft.WindowsAppRuntime.2*';   Label = 'Windows App Runtime 2';   Id = 'Microsoft.WindowsAppRuntime.2.0' },
    @{ Name = 'Microsoft.WindowsAppRuntime.1.8*'; Label = 'Windows App Runtime 1.8'; Id = 'Microsoft.WindowsAppRuntime.1.8' }
)) {
    if (-not (Get-AppxPackage $war.Name)) {
        Write-Error ("{0} is not installed. Install it with:`n    winget install --id {1}`nthen re-run." -f $war.Label, $war.Id)
        exit 1
    }
}
$nugetLocal = Join-Path (Split-Path $here -Parent) "nuget-local"
$sdkNupkg = Get-ChildItem -Path $nugetLocal -Filter "AionInstructPreview.Text.Framework*.nupkg" -ErrorAction SilentlyContinue
if (-not $sdkNupkg) {
    Write-Error "SDK NuGet package not found in $nugetLocal. Run .\Bootstrap.ps1 from the repo root first (it downloads the SDK), or follow the 'Getting the SDK NuGet' section of the README."
    exit 1
}

dotnet run --project (Join-Path $here "AionInstructPreview.Chat.Wpf.csproj") -c Release
