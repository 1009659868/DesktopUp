#requires -Version 5.1
param(
    [string]$AppDirectory,
    [string]$FixtureDirectory,
    [string]$OutputRoot,
    [int[]]$PosterCounts = @(0,1,3),
    [switch]$FullConfiguration,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskPackage = Join-Path $taskRepo 'out\verify\v3-04-06'
if (!$AppDirectory) { $AppDirectory = Join-Path $taskPackage 'app' }
if (!$FixtureDirectory) { $FixtureDirectory = Join-Path $taskPackage 'fixture' }
if (!$OutputRoot) { $OutputRoot = $taskPackage }
$taskApp = [IO.Path]::GetFullPath($AppDirectory)
$taskFixture = [IO.Path]::GetFullPath($FixtureDirectory)
$taskOutput = [IO.Path]::GetFullPath($OutputRoot)
$taskUserRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
if ($taskOutput.Equals($taskUserRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $taskOutput.StartsWith($taskUserRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The process gate cannot use the ordinary DesktopUp data profile.'
}
if (@($PosterCounts | Where-Object { $_ -notin @(0,1,3) }).Count -or $PosterCounts.Count -eq 0) {
    throw 'Only isolated 0/1/3 scenarios are supported.'
}
$taskExecutable = Join-Path $taskApp 'DesktopUp.exe'
$taskFixtureExecutable = Join-Path $taskFixture 'DesktopUp.SessionFixture.exe'
$taskAppAssembly = Join-Path $taskApp 'DesktopUp.dll'
$taskFixtureAssembly = Join-Path $taskFixture 'DesktopUp.SessionFixture.dll'
foreach ($taskRequired in @($taskExecutable, $taskFixtureExecutable, $taskAppAssembly, $taskFixtureAssembly)) {
    if (!(Test-Path -LiteralPath $taskRequired -PathType Leaf)) { throw ('Missing candidate artifact: ' + $taskRequired) }
}
$taskExeHash = (Get-FileHash -LiteralPath $taskExecutable -Algorithm SHA256).Hash
$taskAppHash = (Get-FileHash -LiteralPath $taskAppAssembly -Algorithm SHA256).Hash
$taskFixtureHash = (Get-FileHash -LiteralPath $taskFixtureAssembly -Algorithm SHA256).Hash
if ($CheckOnly) {
    [pscustomobject]@{ Result='READY - NOT EXECUTED'; AppDirectory=$taskApp; FixtureDirectory=$taskFixture;
        OutputRoot=$taskOutput; AppExeHash=$taskExeHash; AppHash=$taskAppHash; FixtureHash=$taskFixtureHash }
    return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) {
    throw 'Exit existing DesktopUp normally first. No process was stopped.'
}
$taskEvidence = Join-Path $taskOutput ('gates-' + [Guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $taskEvidence) { throw 'The evidence root must be new.' }
$null = New-Item -ItemType Directory -Path $taskEvidence
$taskResults = @()
function Invoke-PortableGate {
    param([int]$Count, [string]$Data, [switch]$Restart)
    $taskFlags = '--portable-smoke --exit-after-ms=70000'
    if ($FullConfiguration) { $taskFlags += ' --portable-full' }
    if ($Restart) { $taskFlags += ' --portable-restart' }
    $taskLog = Join-Path $Data 'logs\desktopup.jsonl'
    $taskPreviousLines = if (Test-Path -LiteralPath $taskLog) { [IO.File]::ReadAllLines($taskLog).Count } else { 0 }
    $taskProcess = Start-DesktopUpFixtureProcess -ExecutablePath $taskExecutable -DataRoot $Data -Arguments $taskFlags -WindowStyle Hidden
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(80)
    while (!$taskProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw "Timed out; process preserved. Exit normally: $($taskProcess.Id)" }
    }
    if (!(Test-Path -LiteralPath $taskLog)) { throw ('No process log was written: ' + $Data) }
    $taskEvents = @(Get-Content -LiteralPath $taskLog | Select-Object -Skip $taskPreviousLines | ForEach-Object { $_ | ConvertFrom-Json })
    $taskDone = @($taskEvents | Where-Object eventName -eq 'contract.portable_smoke')
    $taskStopped = @($taskEvents | Where-Object eventName -eq 'application.stopped')
    $taskErrors = @($taskEvents | Where-Object { $_.level -in @('error','critical','fatal') -or $_.data.passed -eq $false })
    $taskError = if ($taskDone.Count) { $taskDone[-1].data.error } else { 'Missing completion event.' }
    if ($taskProcess.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or !$taskDone[0].data.passed -or
        $taskStopped.Count -ne 1 -or $taskStopped[0].data.activeResources -ne 0 -or $taskErrors.Count -ne 0) {
        throw "Portable gate failed for $Count / restart=$Restart : $Data ; $taskError"
    }
    if ($taskDone[0].data.initialCount -ne $Count -or [bool]$taskDone[0].data.restart -ne [bool]$Restart -or
        $taskDone[0].data.checks -lt 1 -or (!$Restart -and @($taskDone[0].data.cases).Count -lt 7)) {
        throw 'The completion evidence does not match the requested count, mode or complete case list.'
    }
    if ($FullConfiguration -and !$Restart -and
        @($taskDone[0].data.cases) -notcontains 'full-template-catalog-instances-hidden-title-and-theme-seeded') {
        throw 'The complete-configuration setup did not run.'
    }
    if ((Get-FileHash -LiteralPath $taskExecutable -Algorithm SHA256).Hash -ne $taskExeHash -or
        (Get-FileHash -LiteralPath $taskAppAssembly -Algorithm SHA256).Hash -ne $taskAppHash -or
        (Get-FileHash -LiteralPath $taskFixtureAssembly -Algorithm SHA256).Hash -ne $taskFixtureHash) {
        throw 'Candidate artifacts changed during the gate; results cannot be bound to one build.'
    }
    [pscustomobject]@{Result='PASS - AUTOMATED CONTROLLER/RUNTIME ONLY'; Posters=$Count; Restart=[bool]$Restart;
        ProcessId=$taskProcess.Id; ExitCode=$taskProcess.ExitCode; Checks=$taskDone[0].data.checks;
        Cases=@($taskDone[0].data.cases); ResourcesAfterExit=$taskStopped[0].data.activeResources;
        DataRoot=$Data; FullConfiguration=[bool]$FullConfiguration; NativePicker=$false; NativeMouse=$false; RealCrossHost=$false;
        AppExeHash=$taskExeHash; AppHash=$taskAppHash; FixtureHash=$taskFixtureHash}
}
foreach ($taskCount in ($PosterCounts | Sort-Object -Unique)) {
    $taskData = Join-Path $taskEvidence ([string]$taskCount)
    if ($taskCount -eq 0) { & $taskFixtureExecutable $taskData 0 | Out-Null }
    else { & $taskFixtureExecutable $taskData $taskCount --measure-expanded | Out-Null }
    if ($LASTEXITCODE -ne 0) { throw 'Fixture creation failed. No existing data was overwritten.' }
    $taskItem = Invoke-PortableGate -Count $taskCount -Data $taskData
    $taskResults += $taskItem; $taskItem
    $taskItem = Invoke-PortableGate -Count $taskCount -Data $taskData -Restart
    $taskResults += $taskItem; $taskItem
}
$taskResults | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskEvidence 'results.json') -Encoding UTF8
if ($taskResults.Count -eq 6 -and (($PosterCounts | Sort-Object -Unique) -join ',') -eq '0,1,3') {
    $taskResults | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'portable-gates-passed.json') -Encoding UTF8
}
