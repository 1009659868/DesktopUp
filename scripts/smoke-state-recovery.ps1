#requires -Version 5.1
param([string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskPackage = if ($PackageDirectory) { [IO.Path]::GetFullPath($PackageDirectory) } else { Join-Path $taskRepo 'out\verify\v3-04-06' }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit existing DesktopUp normally first. No process was stopped.' }
$taskExe = Join-Path $taskPackage 'app\DesktopUp.exe'
$taskFixture = Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe'
$taskHash = (Get-FileHash -LiteralPath (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash
$taskRoot = Join-Path $taskPackage ('recovery-gates-' + [Guid]::NewGuid().ToString('N'))
$taskResults = @()
foreach ($taskCase in @('healthy','corrupt','pending')) {
    $taskData = Join-Path $taskRoot $taskCase
    & $taskFixture $taskData 1 --measure-expanded | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not create new isolated recovery fixture.' }
    $taskDb = Join-Path $taskData 'state\desktopup.sqlite3'
    $taskBackup = Join-Path $taskData 'state\backups\fixture-good.sqlite3'
    # Fixture owns this NEW directory; there is no running SQLite connection or WAL.
    if ((Test-Path -LiteralPath ($taskDb + '-wal')) -or (Test-Path -LiteralPath ($taskDb + '-shm'))) { throw 'Fixture has outstanding SQLite sidecars.' }
    Copy-Item -LiteralPath $taskDb -Destination $taskBackup
    if ($taskCase -eq 'corrupt') {
        Move-Item -LiteralPath $taskDb -Destination (Join-Path $taskData 'state\fixture-original.sqlite3')
        [IO.File]::WriteAllBytes($taskDb, [Text.Encoding]::UTF8.GetBytes('isolated intentionally corrupt SQLite fixture'))
    }
    if ($taskCase -eq 'pending') {
        # Presence guard only. Actual journal stage/rollback correctness has separate fault tests.
        [IO.File]::WriteAllText((Join-Path $taskData 'state\recovery-pending.json'), '{}')
    }
    $taskBefore = (Get-FileHash -LiteralPath $taskDb).Hash
    $taskBackupBefore = (Get-FileHash -LiteralPath $taskBackup).Hash
    $taskArgs = '--recovery-smoke'
    if ($taskCase -eq 'healthy') { $taskArgs += ' --recover-state' }
    $taskProcess = Start-DesktopUpFixtureProcess -ExecutablePath $taskExe -DataRoot $taskData -Arguments $taskArgs -WindowStyle Hidden
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(80)
    while (!$taskProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw "Recovery gate timed out; process preserved: $($taskProcess.Id)" }
    }
    $taskEvents = @(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $taskDone = @($taskEvents | Where-Object eventName -eq 'contract.recovery_smoke')
    $taskStop = @($taskEvents | Where-Object eventName -eq 'application.stopped')
    $taskUnexpected = @($taskEvents | Where-Object { $_.level -in @('error','critical','fatal') -and
        !($taskCase -eq 'corrupt' -and $_.eventName -in @('state_store.initialization_failed','application.state_store_unavailable')) })
    if ($taskCase -eq 'corrupt') {
        # This fixture deliberately contains non-SQLite bytes: both layers must report error 26.
        $taskInitialization = @($taskEvents | Where-Object eventName -eq 'state_store.initialization_failed')
        $taskUnavailable = @($taskEvents | Where-Object eventName -eq 'application.state_store_unavailable')
        if ($taskInitialization.Count -ne 1 -or $taskUnavailable.Count -ne 1 -or
            $taskInitialization[0].data.Message -notmatch 'SQLite Error 26' -or
            $taskUnavailable[0].data.Error -notmatch 'SQLite Error 26') { throw 'Missing expected corrupt-database diagnostics.' }
    }
    $taskUnchanged = (Get-FileHash -LiteralPath $taskDb).Hash -eq $taskBefore -and
        (Get-FileHash -LiteralPath $taskBackup).Hash -eq $taskBackupBefore
    if ($taskProcess.ExitCode -ne 0 -or $taskDone.Count -ne 1 -or !$taskDone[0].data.passed -or
        $taskDone[0].data.snapshot.ValidCount -lt 1 -or $taskStop.Count -ne 1 -or
        $taskStop[0].data.activeResources -ne 0 -or !$taskUnchanged -or $taskUnexpected.Count -ne 0) {
        throw "Recovery window gate failed: $taskCase; evidence preserved in $taskData"
    }
    if ($taskCase -eq 'pending' -and (!$taskDone[0].data.snapshot.Pending -or
        [IO.File]::ReadAllText((Join-Path $taskData 'state\recovery-pending.json')) -ne '{}')) { throw 'Pending journal was ignored or modified.' }
    if ((Get-FileHash -LiteralPath (Join-Path $taskPackage 'app\DesktopUp.dll')).Hash -ne $taskHash) { throw 'Candidate changed during gate.' }
    $taskResult = [pscustomobject]@{Result='PASS - OFFLINE WINDOW ONLY'; Case=$taskCase; AppHash=$taskHash;
        StateUnchanged=$taskUnchanged; ResourcesAfterExit=$taskStop[0].data.activeResources; ProcessId=$taskProcess.Id;
        DataRoot=$taskData; NativeConfirmation=$false; RestoreExecuted=$false; ActualPowerLoss=$false}
    $taskResults += $taskResult; $taskResult
}
$taskResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot 'results.json') -Encoding UTF8
$taskResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskPackage 'recovery-gates-passed.json') -Encoding UTF8
