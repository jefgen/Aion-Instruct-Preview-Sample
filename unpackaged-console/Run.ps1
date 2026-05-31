# Run.ps1 -- build and run the unpackaged CONSOLE chat sample.
#
# Same model/runtime prerequisites as the WPF sample. This app streams the
# model's reply to stdout so terminal output can be captured directly.
#
# Usage:
#   .\Run.ps1                       # default prompt, output to the console
#   .\Run.ps1 "your prompt here"    # custom prompt
#   .\Run.ps1 -CaptureLog out.log   # tee stdout+stderr (merged) to a file for
#                                   # grepping native noise non-interactively
#
# Use -CaptureLog to save stdout and stderr for inspection.

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Prompt,
    [string] $CaptureLog
)

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

$csproj = Join-Path $here "AionInstructPreview.Chat.Console.csproj"

# Build first so build output never pollutes the captured run.
dotnet build $csproj -c Release | Out-Host

# Run the built executable directly so captured output contains only the sample app.
$exe = Get-ChildItem -Path (Join-Path $here "bin\Release") -Filter "AionInstructPreview.Chat.Console.exe" -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $exe) {
    Write-Error "Build succeeded but AionInstructPreview.Chat.Console.exe was not found under bin\Release."
    exit 1
}

$argList = @()
if ($Prompt) { $argList = $Prompt }

if ($CaptureLog) {
    Write-Host "Capturing stdout+stderr to $CaptureLog ..."
    & $exe.FullName @argList *> $CaptureLog
    Write-Host "--- captured output ---"
    Get-Content $CaptureLog
} else {
    & $exe.FullName @argList
}
