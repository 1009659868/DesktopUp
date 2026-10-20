#requires -Version 5.1
# R1 isolated mapping / observation review. No installation, startup changes or desktop samples.
param(
    [ValidateSet('Open','Prepare','Probe','Status')][string]$Action='Open',
    [ValidateRange(1,99)][int]$ProbeAttempt=1,
    [ValidateSet('R1','R7','P1','P2','P3')][string]$Batch='R1'
)
$ErrorActionPreference='Stop'
$r1Repo=Split-Path -Parent $PSScriptRoot
$r1Product=Join-Path $r1Repo 'out/architecture-r1-observation/product02'
$r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-observation-review01'
$r1Fixture=Join-Path $r1Repo 'out/architecture-r1-observation/fixture/DesktopUp.SessionFixture.exe'
if($Action -eq 'Probe'){
    $r1Product=Join-Path $r1Repo 'out/architecture-r1-observation/review02'
    $r1Review=Join-Path $r1Repo ('out/drop-d3/architecture-r1-observation-probe'+$ProbeAttempt.ToString('00'))
}
if($Batch -in @('R7','P1','P2','P3')){
    $r1Area=switch($Batch){'P3'{'architecture-p3-directory'} 'P2'{'architecture-p2-discovery'} 'P1'{'architecture-p1-shared'} default{'architecture-r7-cost'}}
    $r1CandidateName=if($Batch -in @('P2','P3')){'candidate01'}else{'candidate02'}
    $r1Candidate=Join-Path $r1Repo ("out/"+$r1Area+"/"+$r1CandidateName)
    $r1Product=Join-Path $r1Candidate 'app'
    $r1Fixture=Join-Path $r1Candidate 'fixture/DesktopUp.SessionFixture.exe'
    $r1Review=Join-Path $r1Repo ("out/drop-d3/"+$r1Area+"-review01")
    if($Action -eq 'Probe'){
        $r1Product=Join-Path $r1Candidate 'review'
        $r1Review=Join-Path $r1Repo ('out/drop-d3/'+$r1Area+'-probe'+$ProbeAttempt.ToString('00'))
    }
}
$r1Profile=Join-Path $r1Review 'profile'
$r1App=Join-Path $r1Product 'DesktopUp.exe'
$r1Ready=Join-Path $r1Review '.r1-observation-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$r1App;Profile=$r1Profile;Prepared=(Test-Path -LiteralPath $r1Ready)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit the running DesktopUp normally first. Nothing stopped.'}
foreach($r1Path in @($r1App,$r1Fixture,(Join-Path $r1Product 'content-worker/DesktopUp.ContentWorker.exe'))){
    if(!(Test-Path -LiteralPath $r1Path -PathType Leaf)){throw "Missing R1 build: $r1Path"}
}
if($Action -ne 'Probe'){
    $r1Hashes=@{
        'DesktopUp.dll'='4223312FC1482039CE5387683CFEBEDFF4176FD105EC274725F4B6F03F9C01B0'
        'DesktopUp.Application.dll'='FCBFA39629E0EF59989F80C4C0E9EC93D4A2A485B7CCB05C7E93099097F81054'
        'DesktopUp.Infrastructure.dll'='A2019C030BB96DC4FED173B1A0DB5F06663604A4505244D4EACB87AA4D55C060'
    }
    if($Batch -in @('R7','P1','P2','P3')){
        $r1Saved=Get-Content -LiteralPath (Join-Path $r1Candidate 'candidate-hashes.json') -Raw | ConvertFrom-Json
        $r1Hashes=@{}
        $r1Saved.PSObject.Properties | ForEach-Object {$r1Hashes[$_.Name]=$_.Value}
    }
    foreach($r1Name in $r1Hashes.Keys){
        if((Get-FileHash -LiteralPath (Join-Path $r1Product $r1Name)).Hash -cne $r1Hashes[$r1Name]){
            throw "R1 candidate changed: $r1Name. Do not reuse the previous acceptance record."
        }
    }
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$r1Tools=Enter-DesktopUpToolchain -Offline
try {
    if(!(Test-Path -LiteralPath $r1Review)){
        New-Item -ItemType Directory -Path $r1Review | Out-Null
        [IO.File]::WriteAllText((Join-Path $r1Review '.incoming-fixture'),'owned')
        & $r1Fixture $r1Profile --content-layout empty | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed; evidence preserved, no automatic overwrite.'}
        if($Action -ne 'Probe'){
            $r1Samples=Join-Path $r1Review 'mapping-input'
            New-Item -ItemType Directory -Path $r1Samples | Out-Null
            [IO.File]::WriteAllText((Join-Path $r1Samples 'mapped-sample.txt'),'R1 owned mapping sample')
            [IO.File]::WriteAllText((Join-Path $r1Review 'ordinary-sample.txt'),'R1 owned managed sample')
            New-Item -ItemType Directory -Path (Join-Path $r1Review 'external-target') | Out-Null
            if($Batch -in @('P2','P3')){
                New-Item -ItemType Directory -Path (Join-Path $r1Review 'storage-target') | Out-Null
            }
            if($Batch -eq 'P3'){
                $r1Second=Join-Path $r1Review 'second-mapping'
                New-Item -ItemType Directory -Path $r1Second | Out-Null
                [IO.File]::WriteAllText((Join-Path $r1Second 'second-sample.txt'),'P3 owned second mapping')
            }
        }
        [IO.File]::WriteAllText($r1Ready,'owned')
    }
    if(!(Test-Path -LiteralPath $r1Ready) -or [IO.File]::ReadAllText($r1Ready) -ne 'owned'){
        throw 'Not a completed owned R1 fixture. Nothing overwritten.'
    }
    if($Action -eq 'Prepare'){
        [pscustomobject]@{Profile=$r1Profile;Samples=$r1Review;Executable=$r1App}; return
    }
    if($Action -eq 'Probe'){
        $r1Log=Join-Path $r1Profile 'logs/desktopup.jsonl'
        if(Test-Path -LiteralPath $r1Log){throw 'Probe data already used. Choose a new ProbeAttempt; previous evidence preserved.'}
        foreach($r1Restart in @($false,$true)){
            if($r1Restart){
                # The first child exited cleanly. Add a new owned file while the app is stopped.
                $r1Offline=Join-Path $r1Review 'center-map-probe/offline-added.txt'
                if(Test-Path -LiteralPath $r1Offline){throw 'Offline sample already exists; refusing overwrite.'}
                [IO.File]::WriteAllText($r1Offline,'R1 owned offline sample')
            }
            $r1Args='--incoming-probe --r1-observation-probe'
            if($r1Restart){$r1Args+=' --incoming-restart'}
            $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments $r1Args -WindowStyle Hidden
            if(!$r1Process.WaitForExit(60000)){throw "Probe timed out; owned PID $($r1Process.Id) preserved for diagnosis."}
            $r1Events=@(Get-Content -LiteralPath $r1Log | ForEach-Object {$_|ConvertFrom-Json})
            if($r1Process.ExitCode -ne 0 -or !($r1Events | Where-Object { $_.eventName -eq 'contract.r1_observation_passed' -and $_.data.restart -eq $r1Restart })){
                throw "R1 observation probe failed. See $r1Log"
            }
        }
        [pscustomobject]@{Probe='PASS';Profile=$r1Profile;NativeMouse=$false;Restart=$true}
        return
    }
    $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$r1Process.Id;Executable=$r1App;Profile=$r1Profile;Samples=$r1Review;ReviewExecutors=$false}
} finally {Exit-DesktopUpToolchain $r1Tools}
