#requires -Version 5.1
# R2: reuse owned real-file fixture for manual review; two bounded probes use separate data.
param(
    [ValidateSet('Open','Prepare','Status','Probe')][string]$Action='Open',
    [ValidateSet('Session','Source')][string]$Probe='Session'
)
$ErrorActionPreference='Stop'
$r2Repo=Split-Path -Parent $PSScriptRoot
$r2Candidate=Join-Path $r2Repo 'out/architecture-r2-session/candidate01'
$r2Product=Join-Path $r2Candidate 'app'
$r2Review=Join-Path $r2Repo 'out/drop-d3/architecture-r2-session-review01'
$r2Fixture=Join-Path $r2Candidate 'fixture/DesktopUp.SessionFixture.exe'
if($Action -eq 'Probe'){
    $r2Product=Join-Path $r2Repo 'out/architecture-r2-session/probes/app'
    $r2Review=Join-Path $r2Repo ('out/drop-d3/architecture-r2-session-probe-'+$Probe.ToLowerInvariant()+'01')
}
$r2App=Join-Path $r2Product 'DesktopUp.exe'
$r2Profile=Join-Path $r2Review 'profile'
$r2Marker=Join-Path $r2Review '.cross-poster-fixture'
$r2Ready=Join-Path $r2Review '.session-review-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$r2App;Profile=$r2Profile;Samples=$r2Review;Prepared=(Test-Path -LiteralPath $r2Ready)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
foreach($r2Path in @($r2App,$r2Fixture)){
    if(!(Test-Path -LiteralPath $r2Path -PathType Leaf)){throw "Missing build: $r2Path"}
}
if($Action -ne 'Probe'){
    $r2Hashes=@{
        'DesktopUp.dll'='8945CF3E008AE02CC764520B6236E1CF3A628537131B775F3480A4DE1161465F'
        'DesktopUp.Application.dll'='CA37EB208BE911CFD41A5655B2AB455D2D6FE23EB9F2D85A92FCA0EBDAE381F3'
    }
    foreach($r2Name in $r2Hashes.Keys){
        if((Get-FileHash -LiteralPath (Join-Path $r2Product $r2Name)).Hash -cne $r2Hashes[$r2Name]){
            throw "Candidate changed: $r2Name. Nothing overwritten."
        }
    }
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$r2Tools=Enter-DesktopUpToolchain -Offline
try {
    if($Action -eq 'Probe'){
        if(Test-Path -LiteralPath $r2Review){throw 'Probe evidence already exists; nothing overwritten.'}
        New-Item -ItemType Directory -Path $r2Review | Out-Null
        if($Probe -eq 'Session'){
            [IO.File]::WriteAllText($r2Marker,'owned')
            [IO.File]::WriteAllText((Join-Path $r2Review '.heat-runtime-fixture'),'owned')
            & $r2Fixture $r2Profile --cross-poster | Out-Null
            $r2Arguments='--content-heat-runtime-probe --r2-session-probe'
            $r2Event='contract.r2_center_release_passed'
        }else{
            & $r2Fixture $r2Profile --content-layout resource-one | Out-Null
            $r2Arguments='--content-source-probe'
            $r2Event='content.source_observed'
        }
        if($LASTEXITCODE-ne 0){throw 'Probe fixture failed; evidence retained.'}
        $r2Hash=(Get-FileHash -LiteralPath (Join-Path $r2Product 'DesktopUp.dll')).Hash
        $r2Process=Start-DesktopUpFixtureProcess -ExecutablePath $r2App -DataRoot $r2Profile -Arguments $r2Arguments -WindowStyle Hidden
        Write-Host "Probe $Probe started: PID $($r2Process.Id)"
        $r2Deadline=[DateTime]::UtcNow.AddSeconds(90)
        while(!$r2Process.WaitForExit(500)){
            if([DateTime]::UtcNow -gt $r2Deadline){throw "Probe timeout; PID $($r2Process.Id) and evidence retained."}
        }
        $r2Events=@(Get-Content -LiteralPath (Join-Path $r2Profile 'logs/desktopup.jsonl') | ForEach-Object {$_|ConvertFrom-Json})
        $r2Done=@($r2Events|Where-Object eventName -eq $r2Event)
        $r2Stopped=@($r2Events|Where-Object eventName -eq 'application.stopped')
        $r2Errors=@($r2Events|Where-Object level -in @('error','critical','fatal'))
        if($r2Process.ExitCode-ne 0 -or $r2Done.Count-ne 1 -or $r2Stopped.Count-ne 1 -or
            $r2Stopped[0].data.activeResources-ne 0 -or $r2Errors.Count){throw "Probe failed; inspect $r2Profile"}
        if((Get-FileHash -LiteralPath (Join-Path $r2Product 'DesktopUp.dll')).Hash -cne $r2Hash){throw 'Probe build changed.'}
        $r2Result=[pscustomobject]@{Result='PASS';Probe=$Probe;PID=$r2Process.Id;ResourcesAfterExit=0;NativeMouse=$false;AppHash=$r2Hash}
        $r2Result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $r2Review 'results.json') -Encoding UTF8
        $r2Result
        return
    }
    if(!(Test-Path -LiteralPath $r2Review)){
        New-Item -ItemType Directory -Path $r2Review | Out-Null
        [IO.File]::WriteAllText($r2Marker,'owned')
        & $r2Fixture $r2Profile --cross-poster | Out-Null
        if($LASTEXITCODE-ne 0){throw 'Fixture creation failed; partial evidence retained.'}
        Get-ChildItem -LiteralPath $r2Review -File -Recurse | Where-Object {$_.FullName -notlike ($r2Profile+'*')} |
            ForEach-Object {Get-FileHash -LiteralPath $_.FullName} |
            ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $r2Review 'sample-manifest.json') -Encoding UTF8
        [IO.File]::WriteAllText($r2Ready,'owned')
    }
    if(!(Test-Path -LiteralPath $r2Ready) -or [IO.File]::ReadAllText($r2Marker)-ne 'owned'){
        throw 'Not a completed owned review. Nothing overwritten.'
    }
    if($Action -eq 'Prepare'){[pscustomobject]@{Profile=$r2Profile;Samples=$r2Review};return}
    $r2Process=Start-DesktopUpFixtureProcess -ExecutablePath $r2App -DataRoot $r2Profile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$r2Process.Id;Profile=$r2Profile;Samples=$r2Review;InstallerExecuted=$false}
}finally{Exit-DesktopUpToolchain $r2Tools}
