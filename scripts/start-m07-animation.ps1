#requires -Version 5.1
param([switch]$CheckOnly, [switch]$Smoke, [string]$AnimationConfig)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskRepo 'out\verify\v3-04-m07-animation'
$taskApp = Join-Path $taskBuild 'app\DesktopUp.exe'
$taskFixture = Join-Path $taskBuild 'fixture\DesktopUp.SessionFixture.dll'
$taskExpected = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'm07-animation-build.json') -Raw | ConvertFrom-Json
foreach ($taskArea in $taskExpected.PSObject.Properties) {
    foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
        $taskFile = Join-Path (Join-Path $taskBuild $taskArea.Name) $taskEntry.Name
        if (!(Test-Path -LiteralPath $taskFile) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
            throw "Animation artifact fingerprint mismatch: $taskFile"
        }
    }
}
if ([string]::IsNullOrWhiteSpace($AnimationConfig)) { $AnimationConfig = Join-Path $taskRepo 'config\poster-animation.json' }
$taskConfig = (Get-Item -LiteralPath $AnimationConfig).FullName
if ($taskConfig.Contains('"')) { throw 'Invalid configuration path.' }
$taskValidated = & dotnet $taskFixture --validate-animation-config $taskConfig
if ($LASTEXITCODE -ne 0) { throw 'Animation configuration is invalid; no app or fixture was started.' }
$taskOptions = $taskValidated | ConvertFrom-Json
if ($CheckOnly) {
    [pscustomobject]@{ Result='PREFLIGHT ONLY'; App=$taskApp; AnimationConfig=$taskConfig; Options=$taskOptions; PowerShell=$PSVersionTable.PSVersion.ToString() }
    return
}
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the previous DesktopUp normally before starting this isolated copy.' }
$taskParent = Join-Path $taskRepo 'out\manual\v3-04-m07-animation'
$null = New-Item -ItemType Directory -Path $taskParent -Force
$taskData = Join-Path $taskParent ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
& dotnet $taskFixture $taskData 3 --measure-collapsed
if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture creation failed.' }
$taskArgs = $(if ($Smoke) { '--continuity-smoke --exit-after-ms=30000' } else { '--continuity-arrange' })
$taskArgs += ' --animation-config="' + $taskConfig + '"'
$taskProcess = Start-DesktopUpFixtureProcess -ExecutablePath $taskApp -DataRoot $taskData -Arguments $taskArgs -WindowStyle Normal
if ($Smoke) {
    $taskProcess.WaitForExit()
    $taskRows = @(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $taskFinal = @($taskRows | Where-Object { $_.eventName -eq 'continuity.smoke_completed' })
    $taskErrors = @($taskRows | Where-Object { $_.level -in @('error','critical') })
    $taskFrames = @($taskRows | Where-Object { $_.eventName -eq 'spatial.probe_frame' })
    $taskLedger = Get-Content -LiteralPath (Join-Path $taskData 'state\resource-ledger.json') -Raw | ConvertFrom-Json
    if ($taskProcess.ExitCode -ne 0 -or $taskFinal.Count -ne 1 -or !$taskFinal[0].data.passed -or $taskErrors.Count -ne 0 -or
        !$taskLedger.clean -or @($taskLedger.resources).Count -ne 0 -or $taskFrames.Count -eq 0 -or
        @($taskFrames | Where-Object { !$_.data.applied -or $_.data.maximumEdgeDelta -gt $taskOptions.MaximumStepPixels }).Count -ne 0) {
        throw "Animation probe failed; inspect $taskData"
    }
}
[pscustomobject]@{
    Result=$(if ($Smoke) { 'NATIVE ANIMATION PASS - VISUAL VALIDATION PENDING' } else { 'STARTED - ANIMATION MANUAL PENDING' })
    ProcessId=$taskProcess.Id; DataRoot=$taskData; LogPath=(Join-Path $taskData 'logs\desktopup.jsonl')
    AnimationConfig=$taskConfig; Options=$taskOptions
}
