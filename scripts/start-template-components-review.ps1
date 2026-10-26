#requires -Version 5.1
param([ValidateSet('Probe','Open','Status')][string]$Action='Open',
    [ValidateRange(1,99)][int]$Run=1)
$ErrorActionPreference='Stop'
$componentRepo=Split-Path -Parent $PSScriptRoot
$componentMode=if($Action -eq 'Probe'){'probe'}else{'review'}
$componentRoot=Join-Path $componentRepo ('out/drop-d3/template-components-'+$componentMode+$Run.ToString('00'))
$componentProfile=Join-Path $componentRoot 'profile'
$componentBuild=if($Action -eq 'Probe'){'probe'}else{'candidate'}
$componentApp=Join-Path $componentRepo ('out/joint-template/'+$componentBuild+'/app/DesktopUp.exe')
$componentFixture=Join-Path $componentRepo 'tests/DesktopUp.SessionFixture/bin/x64/Release/net10.0-windows10.0.22621.0/DesktopUp.SessionFixture.exe'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$componentApp;Profile=$componentProfile;Prepared=(Test-Path -LiteralPath (Join-Path $componentRoot '.template-components-fixture'))};return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
foreach($componentFile in @($componentApp,$componentFixture)){
    if(!(Test-Path -LiteralPath $componentFile -PathType Leaf)){throw "Build missing: $componentFile"}
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$componentTools=Enter-DesktopUpToolchain -Offline
try{
    if($Action -eq 'Probe' -and (Test-Path -LiteralPath $componentRoot)){throw 'Use a new Run; existing evidence preserved.'}
    if(!(Test-Path -LiteralPath $componentRoot)){
        New-Item -ItemType Directory -Path $componentRoot | Out-Null
        [IO.File]::WriteAllText((Join-Path $componentRoot '.model-protocol-fixture'),'owned')
        & $componentFixture $componentProfile --template-components | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Fixture initialization failed; preserved.'}
        [IO.File]::WriteAllText((Join-Path $componentRoot '.template-components-fixture'),'owned')
        [IO.File]::WriteAllText((Join-Path $componentRoot '.heat-runtime-fixture'),'owned')
    }
    if([IO.File]::ReadAllText((Join-Path $componentRoot '.template-components-fixture')) -ne 'owned'){throw 'Not an owned component fixture.'}
    $componentArguments=if($Action -eq 'Probe'){'--real-activity-probe --template-components-probe'}else{'--show-control-center'}
    $componentProcess=Start-DesktopUpFixtureProcess -ExecutablePath $componentApp -DataRoot $componentProfile -Arguments $componentArguments -WindowStyle Hidden
    if($Action -eq 'Open'){
        [pscustomobject]@{PID=$componentProcess.Id;Profile=$componentProfile;Status='USER PENDING'};return
    }
    if(!$componentProcess.WaitForExit(55000)){throw "Probe timeout; PID $($componentProcess.Id); preserve evidence."}
    $componentEvents=@(Get-Content -LiteralPath (Join-Path $componentProfile 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    foreach($componentEvent in @('contract.template_components_passed','contract.real_activity_runtime_passed','application.stopped')){
        if(@($componentEvents | Where-Object eventName -eq $componentEvent).Count -ne 1){throw "Missing/duplicate event: $componentEvent"}
    }
    $componentStopped=$componentEvents | Where-Object eventName -eq 'application.stopped'
    if($componentProcess.ExitCode -ne 0 -or $componentStopped.data.activeResources -ne 0 -or
        @($componentEvents | Where-Object level -in @('error','critical','fatal')).Count){throw 'Exit/resource/error gate failed; preserved.'}
    $componentResult=[pscustomobject]@{Passed=$true;PID=$componentProcess.Id;ExitCode=$componentProcess.ExitCode;ActiveResources=$componentStopped.data.activeResources;
        Results=@($componentEvents | Where-Object eventName -like 'contract.*passed')}
    [IO.File]::WriteAllText((Join-Path $componentRoot 'result.json'),($componentResult | ConvertTo-Json -Depth 12))
    $componentResult | ConvertTo-Json -Depth 12
}finally{Exit-DesktopUpToolchain $componentTools}
