#requires -Version 5.1
# R1/R5/R6/R8 isolated review. No installation, startup changes or desktop samples.
param(
    [ValidateSet('Open','Prepare','Probe','Status')][string]$Action='Open',
    [ValidateRange(1,99)][int]$ProbeAttempt=1,
    [ValidateSet('R1','R5','R6','R8')][string]$Batch='R1'
)
$ErrorActionPreference='Stop'
if($Batch -eq 'R8' -and $Action -eq 'Probe'){throw 'R8 has no new behavioral probe. Use Prepare/Open; do not replay an older batch.'}
$r1Repo=Split-Path -Parent $PSScriptRoot
$r1Product=Join-Path $r1Repo 'out/architecture-r1/product01'
$r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-review01'
$r1Fixture=Join-Path $r1Repo 'out/architecture-r1/fixture/DesktopUp.SessionFixture.exe'
if($Action -eq 'Probe'){
    $r1Product=Join-Path $r1Repo 'out/architecture-r1/review01'
    $r1Review=Join-Path $r1Repo ('out/drop-d3/architecture-r1-probe'+$ProbeAttempt.ToString('00'))
}
if($Batch -in @('R5','R6','R8')){
    $r1Slice=switch($Batch){'R5'{'r5-content'} 'R6'{'r6-lifecycle'} 'R8'{'r8-review'}}
    $r1Candidate=Join-Path $r1Repo ("out/architecture-"+$r1Slice+"/candidate01")
    $r1Product=Join-Path $r1Candidate 'app'
    $r1Fixture=Join-Path $r1Candidate 'fixture/DesktopUp.SessionFixture.exe'
    $r1Review=Join-Path $r1Repo ("out/drop-d3/architecture-"+$r1Slice+"-review01")
    if($Batch -eq 'R8'){$r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r8-review01'}
    if($Action -eq 'Probe'){
        $r1Product=Join-Path $r1Candidate 'review'
        $r1Review=Join-Path $r1Repo ("out/drop-d3/architecture-"+$r1Slice+"-probe"+$ProbeAttempt.ToString('00'))
    }
}
$r1Profile=Join-Path $r1Review 'profile'
$r1App=Join-Path $r1Product 'DesktopUp.exe'
$r1Ready=Join-Path $r1Review '.r1-ready'
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
        'DesktopUp.dll'='7AD178F8E36476576668DC384A223C6C368712B91C6A150C468E4B72DE98669B'
        'DesktopUp.Application.dll'='11F2C8DED6A91D124FA707E625706D34DFEC8B9801AD145C1A2BD521E8551C26'
        'DesktopUp.Infrastructure.dll'='5AEE30C15CFE3F395BA0BE56B01DD6F4AE5D90AC661D603F08D8F047D0F41DD5'
    }
    if($Batch -in @('R5','R6','R8')){
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
        if($Action -eq 'Probe'){
            [IO.File]::WriteAllText((Join-Path $r1Review '.incoming-fixture'),'owned')
            & $r1Fixture $r1Profile --content-layout empty | Out-Null
        }else{
            [IO.File]::WriteAllText((Join-Path $r1Review '.cross-poster-fixture'),'owned')
            & $r1Fixture $r1Profile --cross-poster | Out-Null
        }
        if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed; evidence preserved, no automatic overwrite.'}
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
        $r1Arguments='--incoming-probe --r1-file-actions-probe'
        $r1Event='contract.r1_file_actions_passed'
        if($Batch -eq 'R5'){
            $r1Arguments='--incoming-probe --r5-mapped-actions-probe'
            $r1Event='contract.r5_mapped_actions_passed'
        }
        if($Batch -eq 'R6'){
            $r1Arguments='--incoming-probe --r6-lifecycle-probe'
            $r1Event='contract.r6_operation_lifetime_passed'
        }
        $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments $r1Arguments -WindowStyle Hidden
        if(!$r1Process.WaitForExit(45000)){throw "Probe timed out; owned PID $($r1Process.Id) preserved for diagnosis."}
        $r1Events=@(Get-Content -LiteralPath $r1Log | ForEach-Object {$_|ConvertFrom-Json})
        if($r1Process.ExitCode -ne 0 -or !($r1Events | Where-Object eventName -eq $r1Event)){
            throw "R1 UI probe failed. See $r1Log"
        }
        [pscustomobject]@{Probe='PASS';Profile=$r1Profile;NativeMouse=$false}
        return
    }
    $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$r1Process.Id;Executable=$r1App;Profile=$r1Profile;Samples=$r1Review;ReviewExecutors=$false}
} finally {Exit-DesktopUpToolchain $r1Tools}
