#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$FixtureDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$PostureOnly
)
$ErrorActionPreference='Stop'
$heatRepo=Split-Path -Parent $PSScriptRoot
$heatRoot=[IO.Path]::GetFullPath($EvidenceDirectory)
if(!$heatRoot.StartsWith([IO.Path]::GetFullPath((Join-Path $heatRepo 'out/drop-d3'))+'\',[StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $heatRoot)){throw 'New isolated evidence directory required.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. No process stopped.'}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$heatTools=Enter-DesktopUpToolchain -Offline
try {
    New-Item -ItemType Directory -Path $heatRoot | Out-Null
    foreach($marker in @('.heat-runtime-fixture','.cross-poster-fixture')){
        [IO.File]::WriteAllText((Join-Path $heatRoot $marker),'owned')
    }
    $heatProfile=Join-Path $heatRoot 'profile'
    & (Join-Path $FixtureDirectory 'DesktopUp.SessionFixture.exe') $heatProfile --cross-poster | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Fixture creation failed; evidence preserved.'}
    $heatFlag=if($PostureOnly){'--content-raster-posture-probe'}else{'--content-heat-runtime-probe'}
    $heatPassEvent=if($PostureOnly){'contract.posture_rasters_passed'}else{'contract.heat_runtime_passed'}
    $heatProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $AppDirectory 'DesktopUp.exe') -DataRoot $heatProfile -Arguments $heatFlag -WindowStyle Hidden
    $heatDeadline=[DateTime]::UtcNow.AddSeconds(55)
    while(!$heatProcess.WaitForExit(300)){
        if([DateTime]::UtcNow -gt $heatDeadline){throw "Timeout; PID $($heatProcess.Id) retained. Evidence: $heatRoot"}
    }
    $heatProcess.Refresh()
    $heatEvents=@(Get-Content -LiteralPath (Join-Path $heatProfile 'logs/desktopup.jsonl') | ForEach-Object {$_ | ConvertFrom-Json})
    $heatErrors=@($heatEvents | Where-Object level -in @('error','critical','fatal'))
    $heatStopped=@($heatEvents | Where-Object eventName -eq 'application.stopped')
    $heatPassed=@($heatEvents | Where-Object eventName -eq $heatPassEvent)
    if($heatProcess.ExitCode -ne 0 -or $heatErrors.Count -or $heatStopped.Count -ne 1 -or
        $heatStopped[0].data.activeResources -ne 0 -or $heatPassed.Count -ne 1){throw "Heat runtime check failed; inspect $heatProfile/logs"}
    $heatResult=[pscustomobject]@{Result='PASS';PID=$heatProcess.Id;ExitCode=$heatProcess.ExitCode;ActiveResources=0;NativeMouse=$false;Detail=$heatPassed[0].data;Profile=$heatProfile}
    $heatResult | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $heatRoot 'result.json') -Encoding UTF8
    $heatResult
} finally {Exit-DesktopUpToolchain $heatTools}
