#requires -Version 5.1
# Isolated R1 lifecycle review. Maintenance mode only: no installer, registry or desktop samples.
param([ValidateSet('Open','Prepare','Keep','Return','Probe','Status')][string]$Action='Open')
$ErrorActionPreference='Stop'
$r1Repo=Split-Path -Parent $PSScriptRoot
$r1Candidate=Join-Path $r1Repo 'out/architecture-r1-lifecycle/candidate01'
$r1Product=Join-Path $r1Candidate 'app'
$r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-lifecycle-review01'
if($Action -eq 'Probe'){
    $r1Product=Join-Path $r1Candidate 'review'
    $r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-lifecycle-probe01'
}
$r1Profile=Join-Path $r1Review 'profile'
$r1Fixture=Join-Path $r1Candidate 'fixture/DesktopUp.SessionFixture.exe'
$r1App=Join-Path $r1Product 'DesktopUp.exe'
$r1Ready=Join-Path $r1Review '.r1-lifecycle-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$r1App;Profile=$r1Profile;Samples=$r1Review;Prepared=(Test-Path -LiteralPath $r1Ready)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit the running DesktopUp normally first. Nothing stopped.'}
foreach($r1Path in @($r1App,$r1Fixture,(Join-Path $r1Product 'content-worker/DesktopUp.ContentWorker.exe'))){
    if(!(Test-Path -LiteralPath $r1Path -PathType Leaf)){throw "Missing R1 build: $r1Path"}
}
$r1Hashes=@{
    'DesktopUp.dll'='04707F51176C6A2011442105C2A5333983D8F5E55CCC66CEB3AC79ACC9253E96'
    'DesktopUp.Application.dll'='E8625DB1B81A548D8F23DCD2D1E0EEF74C731427E144224A2FC735AD64F7D0E0'
    'DesktopUp.Infrastructure.dll'='9DB3804B54D8FC8CB4131C793979BB560F4D28E6605727D4F910AC31F325B72B'
}
foreach($r1Name in $(if($Action -ne 'Probe'){$r1Hashes.Keys})){
    if((Get-FileHash -LiteralPath (Join-Path $r1Product $r1Name)).Hash -cne $r1Hashes[$r1Name]){
        throw "R1 candidate changed: $r1Name. Do not reuse the previous acceptance record."
    }
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$r1Tools=Enter-DesktopUpToolchain -Offline
try {
    if(!(Test-Path -LiteralPath $r1Review)){
        if($Action -notin @('Open','Prepare','Probe')){throw 'Prepare or Open the owned fixture first.'}
        New-Item -ItemType Directory -Path $r1Review | Out-Null
        if($Action -eq 'Probe'){
            [IO.File]::WriteAllText((Join-Path $r1Review '.incoming-fixture'),'owned')
            & $r1Fixture $r1Profile --content-layout empty | Out-Null
        } else {
            [IO.File]::WriteAllText((Join-Path $r1Review '.content-return-fixture'),'owned')
            & $r1Fixture $r1Profile --architecture-lifecycle | Out-Null
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
        if(Test-Path -LiteralPath $r1Log){throw 'Probe data already used. Evidence preserved; do not overwrite.'}
        foreach($r1Restart in @($false,$true)){
            $r1Args='--incoming-probe --content-return-probe'
            if($r1Restart){$r1Args+=' --incoming-restart'}
            $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments $r1Args -WindowStyle Hidden
            if(!$r1Process.WaitForExit(60000)){throw "Probe timed out; owned PID $($r1Process.Id) preserved for diagnosis."}
            $r1Events=@(Get-Content -LiteralPath $r1Log | ForEach-Object {$_|ConvertFrom-Json})
            if($r1Process.ExitCode -ne 0 -or !($r1Events | Where-Object { $_.eventName -eq 'contract.content_return_passed' -and $_.data.restart -eq $r1Restart })){
                throw "R1 lifecycle probe failed. See $r1Log"
            }
            [pscustomobject]@{Probe='PASS';PID=$r1Process.Id;Restart=$r1Restart;NativeMouse=$false}
        }
        return
    }
    $r1Arguments=switch($Action){
        'Open' {'--show-control-center'}
        'Keep' {'--prepare-uninstall=keep'}
        'Return' {'--prepare-uninstall=return'}
    }
    $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments $r1Arguments -WindowStyle Hidden
    [pscustomobject]@{PID=$r1Process.Id;Mode=$Action;Profile=$r1Profile;Samples=$r1Review;InstallerExecuted=$false}
} finally {Exit-DesktopUpToolchain $r1Tools}
