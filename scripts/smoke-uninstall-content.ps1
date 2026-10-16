#requires -Version 5.1
param([Parameter(Mandatory)][string]$CandidateDirectory,[Parameter(Mandatory)][string]$Name)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if($Name -notmatch '^[a-zA-Z0-9-]+$'){throw 'Simple unique evidence name required.'}
$root=Join-Path $repo ('out/drop-d3/'+$Name)
if(Test-Path -LiteralPath $root){throw 'Existing evidence preserved; choose a new name.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first.'}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$ut=Enter-DesktopUpToolchain -Offline
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    [IO.File]::WriteAllText((Join-Path $root '.uninstall-fixture'),'owned')
    $profile=Join-Path $root 'profile';$app=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
    & (Join-Path $CandidateDirectory 'fixture/DesktopUp.SessionFixture.exe') $profile --uninstall-content
    if($LASTEXITCODE -ne 0){throw 'Fixture failed.'}
    $results=@()
    function Run([string]$Case,[string]$Arguments,[int]$Expected,[string]$Data=$profile){
        $p=Start-DesktopUpFixtureProcess -ExecutablePath $app -DataRoot $Data -Arguments $Arguments
        $end=[DateTime]::UtcNow.AddSeconds(45)
        while(!$p.WaitForExit(250)){if([DateTime]::UtcNow -gt $end){throw "Timeout $Case, PID $($p.Id) retained."}}
        $p.Refresh();if($p.ExitCode -ne $Expected){throw "$Case exited $($p.ExitCode), expected $Expected. Inspect $Data"}
        [pscustomobject]@{Case=$Case;ExitCode=$p.ExitCode;PID=$p.Id;Passed=$true}
    }
    $results+=Run 'keep' '--prepare-uninstall=keep --uninstall-silent' 0
    if(Test-Path -LiteralPath (Join-Path $root 'original/return-file.txt')){throw 'Keep moved a file.'}
    $results+=Run 'silent-return-refused' '--prepare-uninstall=return --uninstall-silent' 32
    if(Test-Path -LiteralPath (Join-Path $root 'original/return-file.txt')){throw 'Silent return moved a file.'}
    $results+=Run 'interactive-return' '--prepare-uninstall=return' 0
    foreach($relative in @('original/return-file.txt','original/return-folder/child.txt')){if(!(Test-Path -LiteralPath (Join-Path $root $relative))){throw "Return missing $relative"}}
    $results+=Run 'repeat-empty' '--prepare-uninstall=return --uninstall-silent' 0
    $results+=Run 'never-started' '--prepare-uninstall=return --uninstall-silent' 0 (Join-Path $root 'never-started')
    $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding UTF8
    $results
} finally {Exit-DesktopUpToolchain $ut}
