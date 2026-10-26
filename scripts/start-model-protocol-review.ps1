#requires -Version 5.1
param([ValidateSet('Probe','Open','Status')][string]$Action='Open',
    [ValidateRange(1,99)][int]$Run=1)
$ErrorActionPreference='Stop'
$modelRepo=Split-Path -Parent $PSScriptRoot
$modelMode=if($Action -eq 'Probe'){'probe'}else{'review'}
$modelRoot=Join-Path $modelRepo ('out/drop-d3/model-protocol-'+$modelMode+$Run.ToString('00'))
$modelProfile=Join-Path $modelRoot 'profile'
$modelBuild=if($Action -eq 'Probe'){'probe'}else{'candidate'}
$modelApp=Join-Path $modelRepo ('out/model-protocol/'+$modelBuild+'/app/DesktopUp.exe')
$modelFixture=Join-Path $modelRepo 'tests/DesktopUp.SessionFixture/bin/x64/Release/net10.0-windows10.0.22621.0/DesktopUp.SessionFixture.exe'
$modelMarker=Join-Path $modelRoot '.model-protocol-fixture'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$modelApp;Profile=$modelProfile;Prepared=(Test-Path -LiteralPath $modelMarker)};return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
foreach($modelFile in @($modelApp,$modelFixture)){
    if(!(Test-Path -LiteralPath $modelFile -PathType Leaf)){throw "Build missing: $modelFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$modelTools=Enter-DesktopUpToolchain -Offline
try{
    if($Action -eq 'Probe' -and (Test-Path -LiteralPath $modelRoot)){throw 'Use a new Run; existing evidence preserved.'}
    if(!(Test-Path -LiteralPath $modelRoot)){
        New-Item -ItemType Directory -Path $modelRoot | Out-Null
        [IO.File]::WriteAllText($modelMarker,'owned')
        & $modelFixture $modelProfile --model-protocol | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Fixture initialization failed; preserved.'}
        [IO.File]::WriteAllText((Join-Path $modelRoot '.heat-runtime-fixture'),'owned')
    }
    if([IO.File]::ReadAllText($modelMarker) -ne 'owned'){throw 'Not an owned model review fixture.'}
    $modelArguments=if($Action -eq 'Probe'){'--real-activity-probe --model-protocol-probe'}else{'--show-control-center'}
    $modelProcess=Start-DesktopUpFixtureProcess -ExecutablePath $modelApp -DataRoot $modelProfile -Arguments $modelArguments -WindowStyle Hidden
    if($Action -eq 'Open'){
        [pscustomobject]@{PID=$modelProcess.Id;Profile=$modelProfile;Samples=$modelRoot;Status='USER PENDING'};return
    }
    if(!$modelProcess.WaitForExit(55000)){throw "Probe timeout; PID $($modelProcess.Id); evidence preserved."}
    $modelEvents=@(Get-Content -LiteralPath (Join-Path $modelProfile 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    foreach($modelEvent in @('contract.model_protocol_passed','contract.real_activity_runtime_passed','application.stopped')){
        if(@($modelEvents | Where-Object eventName -eq $modelEvent).Count -ne 1){throw "Missing/duplicate event: $modelEvent"}
    }
    $modelStopped=$modelEvents | Where-Object eventName -eq 'application.stopped'
    if($modelProcess.ExitCode -ne 0 -or $modelStopped.data.activeResources -ne 0 -or
        @($modelEvents | Where-Object level -in @('error','critical','fatal')).Count){throw 'Exit/resource/error gate failed; preserved.'}
    $modelResult=[pscustomobject]@{Passed=$true;ExitCode=$modelProcess.ExitCode;ActiveResources=$modelStopped.data.activeResources;
        Results=@($modelEvents | Where-Object eventName -like 'contract.*passed')}
    [IO.File]::WriteAllText((Join-Path $modelRoot 'result.json'),($modelResult | ConvertTo-Json -Depth 12))
    $modelResult | ConvertTo-Json -Depth 12
}finally{Exit-DesktopUpToolchain $modelTools}
