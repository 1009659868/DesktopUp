#requires -Version 5.1
param([ValidateSet('three','one','empty','pending','backup','lifecycle')][string]$Scenario='three', [switch]$CheckOnly, [switch]$Recovery,
    [ValidateSet('stable','picker')][string]$Candidate='stable')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskPackage = Join-Path $taskRepo $(if ($Candidate -eq 'picker') { 'out\verify\v3-04-06-picker-r3' } else { 'out\verify\v3-04-06' })
$taskManifestPath = Join-Path $PSScriptRoot $(if ($Candidate -eq 'picker') { 'portable-picker-candidate.json' } else { 'portable-candidate.json' })
if (!(Test-Path -LiteralPath $taskManifestPath -PathType Leaf)) { throw 'Portable candidate manifest is missing; no older build fallback.' }
$taskManifest = Get-Content -Raw -LiteralPath $taskManifestPath | ConvertFrom-Json
$taskChecked = 0
foreach ($taskArea in $taskManifest.PSObject.Properties) {
    if ($taskArea.Name -notin @('app','fixture')) { throw 'Unexpected candidate artifact group.' }
    foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
        if ($taskEntry.Name -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($taskEntry.Name) -or
            $taskEntry.Value -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Invalid candidate artifact descriptor.' }
        $taskFile = Join-Path (Join-Path $taskPackage $taskArea.Name) $taskEntry.Name
        if (!(Test-Path -LiteralPath $taskFile -PathType Leaf) -or
            (Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskEntry.Value) {
            throw "Portable candidate missing/changed: $taskFile. No fallback to an older build."
        }
        $taskChecked++
    }
}
if ($taskChecked -ne 25 -or !$taskManifest.app.'DesktopUp.exe' -or !$taskManifest.app.'DesktopUp.dll' -or
    !$taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or !$taskManifest.app.'config/poster-animation.json') {
    throw 'Expected all 25 bound candidate binaries/configuration files.'
}

# Keep the three independent gate documents: a partial/new run cannot stand in for another gate family.
$taskPortable = Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'portable-gates-passed.json') | ConvertFrom-Json
$taskPortableKeys = @($taskPortable | ForEach-Object { '{0}/{1}' -f $_.Posters, [bool]$_.Restart } | Sort-Object -Unique)
$taskPortableExpected = @(foreach ($taskN in @(0,1,3)) { "$taskN/False"; "$taskN/True" })
$taskPortableDiff = @(Compare-Object ($taskPortableExpected | Sort-Object) $taskPortableKeys)
$taskPortableInvalid = @($taskPortable | Where-Object {
    $_.Result -ne 'PASS - AUTOMATED CONTROLLER/RUNTIME ONLY' -or
    $_.AppExeHash -ne $taskManifest.app.'DesktopUp.exe' -or $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or
    $_.FixtureHash -ne $taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or $_.ResourcesAfterExit -ne 0 -or
    $_.ExitCode -ne 0 -or $_.Checks -lt 1 -or (!$_.Restart -and @($_.Cases).Count -lt 7)
})
if ($taskPortable.Count -ne 6 -or $taskPortableDiff.Count -ne 0 -or $taskPortableInvalid.Count -ne 0) {
    throw "The six portable gates are incomplete or from another candidate: count=$($taskPortable.Count), invalid=$($taskPortableInvalid.Count)."
}

$taskTray = Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'gates-passed.json') | ConvertFrom-Json
$taskTrayKeys = @($taskTray | ForEach-Object { "$($_.Mode)/$($_.Posters)" } | Sort-Object -Unique)
$taskTrayExpected = @(foreach ($taskMode in @('exercise','restart','controls','settings')) { foreach ($taskN in @(0,1,3)) { "$taskMode/$taskN" } })
$taskTrayDiff = @(Compare-Object ($taskTrayExpected | Sort-Object) $taskTrayKeys)
$taskTrayInvalid = @($taskTray | Where-Object {
    $_.Result -ne 'PASS - AUTOMATED ONLY' -or $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or
    $_.FixtureHash -ne $taskManifest.fixture.'DesktopUp.SessionFixture.dll' -or
    $_.AnimationHash -ne $taskManifest.app.'config/poster-animation.json' -or $_.ResourcesAfterExit -ne 0
})
if ($taskTray.Count -ne 12 -or $taskTrayDiff.Count -ne 0 -or $taskTrayInvalid.Count -ne 0) {
    throw "The twelve legacy regression gates are incomplete or from another candidate: count=$($taskTray.Count), invalid=$($taskTrayInvalid.Count)."
}

$taskRecovery = Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'recovery-gates-passed.json') | ConvertFrom-Json
$taskRecoveryKeys = @($taskRecovery | ForEach-Object { $_.Case } | Sort-Object -Unique)
$taskRecoveryDiff = @(Compare-Object @('corrupt','healthy','pending') $taskRecoveryKeys)
$taskRecoveryInvalid = @($taskRecovery | Where-Object {
    $_.Result -ne 'PASS - OFFLINE WINDOW ONLY' -or $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or
    $_.ResourcesAfterExit -ne 0 -or $_.StateUnchanged -ne $true
})
if ($taskRecovery.Count -ne 3 -or $taskRecoveryDiff.Count -ne 0 -or $taskRecoveryInvalid.Count -ne 0) {
    throw "The three offline-recovery gates are incomplete or from another candidate: count=$($taskRecovery.Count), invalid=$($taskRecoveryInvalid.Count)."
}

$taskPickerGates = 0
if ($Candidate -eq 'picker') {
    # PS5.1 emits the parsed JSON array as one pipeline object; avoid wrapping it again.
    $taskPicker = Get-Content -Raw -LiteralPath (Join-Path $taskPackage 'picker-gates-passed.json') | ConvertFrom-Json
    $taskPickerKeys = @($taskPicker | ForEach-Object { $_.Case } | Sort-Object -Unique)
    $taskPickerExpected = @('before-open','before-save','cancel-open','cancel-save','center-open','center-save','exit-open','exit-save')
    $taskPickerDiff = @(Compare-Object ($taskPickerExpected | Sort-Object) $taskPickerKeys)
    $taskPickerInvalid = @($taskPicker | Where-Object {
        $_.Result -ne 'PASS - REAL NATIVE PICKER / PROGRAMMATIC COMMAND' -or
        $_.AppHash -ne $taskManifest.app.'DesktopUp.dll' -or $_.WindowsHash -ne $taskManifest.app.'DesktopUp.Windows.dll' -or
        $_.ExitCode -ne 0 -or $_.ResourcesAfterExit -ne 0 -or $_.DatabaseUnchanged -ne $true -or
        $_.NativePicker -ne $true -or $_.NativeMouse -ne $false -or $_.FileIO -ne $false -or
        ($_.Case -like 'exit-*' -and $_.ExitMs -gt 5000)
    })
    if ($taskPicker.Count -ne 8 -or $taskPickerDiff.Count -ne 0 -or $taskPickerInvalid.Count -ne 0) {
        throw 'The eight native picker gates are incomplete or belong to another candidate.'
    }
    $taskPickerGates = 8
}
$taskManualRoot = if ($Candidate -eq 'picker') { 'out\manual\v3-04-06-picker\' } else { 'out\manual\v3-04-06\' }
$taskData = Join-Path $taskRepo ($taskManualRoot + $Scenario)
if ($CheckOnly) {
    [pscustomobject]@{Result='CANDIDATE CHECKED - MANUAL PENDING'; Scenario=$Scenario; Recovery=[bool]$Recovery; Candidate=$Candidate;
        DataRoot=$taskData; VerifiedArtifacts=$taskChecked; PortableGates=6; RegressionGates=12; RecoveryGates=3;
        NativePickerGates=$taskPickerGates; AppHash=$taskManifest.app.'DesktopUp.dll'}
    return
}
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) {
    throw 'Please exit the existing DesktopUp or recovery window normally first. No process was stopped.'
}
if (!(Test-Path -LiteralPath $taskData)) {
    $taskCount = switch ($Scenario) { 'empty' {0} 'one' {1} 'pending' {1} 'backup' {1} 'lifecycle' {3} default {3} }
    if ($taskCount -eq 0) { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData 0 | Out-Null }
    else { & (Join-Path $taskPackage 'fixture\DesktopUp.SessionFixture.exe') $taskData $taskCount --measure-expanded | Out-Null }
    if ($LASTEXITCODE -ne 0) { throw 'Isolated manual fixture creation failed. Any partial profile was preserved.' }
} elseif (!(Test-Path -LiteralPath $taskData -PathType Container)) {
    throw "The preserved manual data root is not a directory: $taskData"
} elseif (!$Recovery -and !(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)) {
    throw "Incomplete profile preserved, please inspect: $taskData"
}
# Recovery deliberately permits a missing/corrupt live database in an existing isolated root; it must not seed it.
$taskArguments = if ($Recovery) { '--recover-state' } else { '--show-control-center' }
$taskArguments += ' --animation-config="' + (Join-Path $taskPackage 'app\config\poster-animation.json') + '"'
$taskStarted = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskPackage 'app\DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - V3-04-06 MANUAL PENDING'; Scenario=$Scenario; Recovery=[bool]$Recovery; Candidate=$Candidate;
    ProcessId=$taskStarted.Id; DataRoot=$taskData; LogPath=(Join-Path $taskData 'logs\desktopup.jsonl');
    AppHash=$taskManifest.app.'DesktopUp.dll'; Scope='Isolated manual profile; repeated command preserves edits; no ordinary profile changed';
    ImportSample=$(if ($Scenario -eq 'pending') { Join-Path $PSScriptRoot 'fixtures\portable-p4-pending.desktopup.json' } else { $null })}
