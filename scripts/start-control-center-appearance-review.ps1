#requires -Version 5.1
param([string]$CandidateDirectory,[string]$ReviewDirectory)
$ErrorActionPreference='Stop'
$appearanceRepo=Split-Path -Parent $PSScriptRoot
if(!$CandidateDirectory){$CandidateDirectory=Join-Path $appearanceRepo 'out/control-center-appearance/candidate01'}
if(!$ReviewDirectory){$ReviewDirectory=Join-Path $appearanceRepo 'out/drop-d3/appearance-review01'}
$appearanceReview=[IO.Path]::GetFullPath($ReviewDirectory).TrimEnd('\')
$appearanceAllowed=[IO.Path]::GetFullPath((Join-Path $appearanceRepo 'out/drop-d3')).TrimEnd('\')+'\'
if(!$appearanceReview.StartsWith($appearanceAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Review must stay inside repository/out/drop-d3.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
$appearanceApp=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
if(!(Test-Path -LiteralPath $appearanceApp)){throw 'Build the appearance candidate first.'}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$appearanceTools=Enter-DesktopUpToolchain -Offline
try {
    $appearanceMarker=Join-Path $appearanceReview '.appearance-review-ready'
    if(!(Test-Path -LiteralPath $appearanceReview)){
        & (Join-Path $PSScriptRoot 'smoke-incoming.ps1') -AppDirectory (Join-Path $CandidateDirectory 'app') -FixtureDirectory (Join-Path $CandidateDirectory 'fixture') -EvidenceDirectory $appearanceReview -AppearanceLayout | Out-Null
        [IO.File]::WriteAllText($appearanceMarker,'owned')
    }
    if(!(Test-Path -LiteralPath $appearanceMarker) -or [IO.File]::ReadAllText($appearanceMarker) -ne 'owned'){throw 'Incomplete appearance review; existing data preserved. Choose a new review directory.'}
    $appearanceProfile=Join-Path $appearanceReview 'profile'
    $appearanceProcess=Start-DesktopUpFixtureProcess -ExecutablePath $appearanceApp -DataRoot $appearanceProfile -Arguments '--show-control-center' -WindowStyle Normal
    [pscustomobject]@{ProcessId=$appearanceProcess.Id;Profile=$appearanceProfile;Executable=$appearanceApp}
} finally {Exit-DesktopUpToolchain $appearanceTools}
