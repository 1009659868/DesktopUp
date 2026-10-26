#requires -Version 5.1
param([ValidatePattern('^[a-z0-9-]+$')][string]$Name='activity-drag-integration01')
$ErrorActionPreference='Stop'
$probeRepo=Split-Path -Parent $PSScriptRoot
$probeRoot=Join-Path $probeRepo ('out/drop-d3/'+$Name)
$probeProfile=Join-Path $probeRoot 'profile'
$probeApp=Join-Path $probeRepo 'out/activity-drag-integration/probe/app/DesktopUp.exe'
$probeFixture=Join-Path $probeRepo 'tests/DesktopUp.SessionFixture/bin/x64/Release/net10.0-windows10.0.22621.0/DesktopUp.SessionFixture.exe'
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'DesktopUp is running; no process was stopped.'}
if(Test-Path -LiteralPath $probeRoot){throw 'Use a new name; existing evidence is never overwritten.'}
foreach($probeFile in @($probeApp,$probeFixture)){
    if(!(Test-Path -LiteralPath $probeFile -PathType Leaf)){throw "Build input missing: $probeFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$probeTools=Enter-DesktopUpToolchain -Offline
try{
    New-Item -ItemType Directory -Path $probeRoot | Out-Null
    [IO.File]::WriteAllText((Join-Path $probeRoot '.cross-poster-fixture'),'owned')
    & $probeFixture $probeProfile --cross-poster | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Fixture initialization failed; preserved.'}
    [IO.File]::WriteAllText((Join-Path $probeRoot '.heat-runtime-fixture'),'owned')
    $probeSamples=@(Get-ChildItem -LiteralPath $probeRoot -Recurse -File | Where-Object {
        !$_.FullName.StartsWith($probeProfile+'\',[StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object { [pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
    [IO.File]::WriteAllText((Join-Path $probeRoot 'created-files.json'),($probeSamples | ConvertTo-Json -Depth 4))
    $probeProcess=Start-DesktopUpFixtureProcess -ExecutablePath $probeApp -DataRoot $probeProfile `
        -Arguments '--real-activity-probe --activity-drag-integration-probe' -WindowStyle Hidden
    if(!$probeProcess.WaitForExit(55000)){throw "Probe timeout; evidence preserved, PID $($probeProcess.Id)."}
    $probeEvents=@(Get-Content -LiteralPath (Join-Path $probeProfile 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    foreach($probeEvent in @('contract.drag_presentation_passed','contract.activity_drag_integration_passed','contract.real_activity_runtime_passed','application.stopped')){
        if(@($probeEvents | Where-Object eventName -eq $probeEvent).Count -ne 1){throw "Missing/duplicate event: $probeEvent; evidence preserved."}
    }
    $probeStopped=$probeEvents | Where-Object eventName -eq 'application.stopped'
    if($probeProcess.ExitCode -ne 0 -or $probeStopped.data.activeResources -ne 0 -or
        @($probeEvents | Where-Object level -in @('error','critical','fatal')).Count){throw 'Exit/resource/error gate failed; evidence preserved.'}
    foreach($probeSample in $probeSamples){
        if((Get-FileHash -LiteralPath $probeSample.Path -Algorithm SHA256).Hash -ne $probeSample.SHA256){throw "Sample changed: $($probeSample.Path)"}
    }
    $probeResult=[pscustomobject]@{Passed=$true;ExitCode=$probeProcess.ExitCode;ActiveResources=$probeStopped.data.activeResources;
        SampleHashesUnchanged=$true;NativeMouse=$false;MeasuredDisplayFps=$false;
        Results=@($probeEvents | Where-Object eventName -like 'contract.*passed')}
    [IO.File]::WriteAllText((Join-Path $probeRoot 'result.json'),($probeResult | ConvertTo-Json -Depth 12))
    $probeResult | ConvertTo-Json -Depth 12
}finally{Exit-DesktopUpToolchain $probeTools}
