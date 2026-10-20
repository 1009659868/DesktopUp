#requires -Version 5.1
# Isolated source repair review; the optional probe reuses the existing R1 transfer check.
param([ValidateSet('Open','Prepare','Status','Probe')][string]$Action='Open')
$ErrorActionPreference='Stop'
$r1Repo=Split-Path -Parent $PSScriptRoot
$r1Candidate=Join-Path $r1Repo 'out/architecture-r1-references/candidate01'
$r1Product=Join-Path $r1Candidate 'app'
$r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-references-review01'
$r1Fixture=Join-Path $r1Candidate 'fixture/DesktopUp.SessionFixture.exe'
if($Action -eq 'Probe'){
    $r1Product=Join-Path $r1Repo 'out/architecture-r1-references/probes/app'
    $r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-references-probe01'
}
$r1Profile=Join-Path $r1Review 'profile'
$r1App=Join-Path $r1Product 'DesktopUp.exe'
$r1Ready=Join-Path $r1Review '.reference-repair-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$r1App;Profile=$r1Profile;Samples=(Join-Path $r1Review 'samples');Prepared=(Test-Path -LiteralPath $r1Ready)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. Nothing stopped.'}
foreach($r1Path in @($r1App,$r1Fixture)){
    if(!(Test-Path -LiteralPath $r1Path -PathType Leaf)){throw "Missing R1 build: $r1Path"}
}
if($Action -ne 'Probe'){
    $r1Hashes=@{
        'DesktopUp.dll'='30374E8A5BF8E13BEF5142042A2F92D39737D658756D3A5E9025669CFD1ABC9A'
        'DesktopUp.Application.dll'='9B9FC30B4E26E94377E252F21790F4D37778FDD9B9E8B6C6D24E97CEEA72ED86'
        'DesktopUp.Infrastructure.dll'='30024EC7DF23CB8783285C958ED05FB02D1344FF2C39DBBD07E9A261AFD75363'
    }
    foreach($r1Name in $r1Hashes.Keys){
        if((Get-FileHash -LiteralPath (Join-Path $r1Product $r1Name)).Hash -cne $r1Hashes[$r1Name]){
            throw "R1 candidate changed: $r1Name. No fallback or overwrite."
        }
    }
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$r1Tools=Enter-DesktopUpToolchain -Offline
try {
    if($Action -eq 'Probe'){
        if(Test-Path -LiteralPath $r1Review){throw 'Probe evidence already exists; nothing overwritten.'}
        New-Item -ItemType Directory -Path $r1Review | Out-Null
        [IO.File]::WriteAllText((Join-Path $r1Review '.incoming-fixture'),'owned')
        & $r1Fixture $r1Profile --content-layout empty | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Probe fixture failed.'}
        $r1Hash=(Get-FileHash -LiteralPath (Join-Path $r1Product 'DesktopUp.dll')).Hash
        $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments '--incoming-probe --r1-transfers-probe' -WindowStyle Hidden
        $r1Deadline=[DateTime]::UtcNow.AddSeconds(90)
        while(!$r1Process.WaitForExit(500)){
            if([DateTime]::UtcNow -gt $r1Deadline){throw "Probe timeout; PID $($r1Process.Id) and evidence preserved."}
        }
        $r1Events=@(Get-Content -LiteralPath (Join-Path $r1Profile 'logs/desktopup.jsonl') | ForEach-Object {$_|ConvertFrom-Json})
        $r1Done=@($r1Events|Where-Object eventName -eq 'contract.r1_transfers_passed')
        $r1Stopped=@($r1Events|Where-Object eventName -eq 'application.stopped')
        $r1Errors=@($r1Events|Where-Object level -in @('error','critical','fatal'))
        if($r1Process.ExitCode -ne 0 -or $r1Done.Count -ne 1 -or $r1Stopped.Count -ne 1 -or
            $r1Stopped[0].data.activeResources -ne 0 -or $r1Errors.Count){throw "Probe failed; inspect $r1Profile"}
        if((Get-FileHash -LiteralPath (Join-Path $r1Product 'DesktopUp.dll')).Hash -cne $r1Hash){throw 'Probe build changed.'}
        $r1Result=[pscustomobject]@{Result='PASS';PID=$r1Process.Id;ResourcesAfterExit=0;NativeMouse=$false;ExternalExitSimulated=$true;AppHash=$r1Hash}
        $r1Result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $r1Review 'results.json') -Encoding UTF8
        $r1Result
        return
    }
    if(!(Test-Path -LiteralPath $r1Review)){
        New-Item -ItemType Directory -Path $r1Review | Out-Null
        [IO.File]::WriteAllText((Join-Path $r1Review '.reference-repair-fixture'),'owned')
        & $r1Fixture $r1Profile --architecture-references | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Reference fixture failed; evidence preserved.'}
        [IO.File]::WriteAllText($r1Ready,'owned')
    }
    if(!(Test-Path -LiteralPath $r1Ready) -or [IO.File]::ReadAllText($r1Ready) -ne 'owned'){
        throw 'Existing directory is not a completed owned fixture. Nothing overwritten.'
    }
    if($Action -eq 'Prepare'){[pscustomobject]@{Profile=$r1Profile;Samples=(Join-Path $r1Review 'samples')};return}
    $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$r1Process.Id;Profile=$r1Profile;Samples=(Join-Path $r1Review 'samples');InstallerExecuted=$false}
} finally {Exit-DesktopUpToolchain $r1Tools}
