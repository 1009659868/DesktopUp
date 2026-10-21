#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Name = 'uninstall-failure01',
    [switch]$ReviewFailure
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root = Join-Path $repo ('out/releases/' + $Name)
if (Test-Path -LiteralPath $root) { throw 'Existing evidence is preserved; choose a new Name.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first.' }
$build = Get-Content (Join-Path $PackageDirectory 'payload/app/preview-build.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($build.Channel -ne 'Preview') { throw 'Requires a production Preview package, not a fixture channel.' }
$install = Join-Path $root '安装 space'
$profile = Join-Path $root 'profile'
$keyName = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.' + $build.Release
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser', 'Registry64')
. (Join-Path $repo 'scripts/Start-DesktopUpFixtureProcess.ps1')
function Registration {
    $key = $registry.OpenSubKey($keyName)
    if (!$key) { return $null }
    try { return $key.GetValue('InstallLocation') } finally { $key.Dispose() }
}
function Assert-Check([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Run([string]$Executable, [string]$Arguments, [int]$Expected = 0, [switch]$Interactive) {
    $style = if ($Interactive) { 'Normal' } else { 'Hidden' }
    $process = Start-DesktopUpFixtureProcess -ExecutablePath $Executable -DataRoot $profile -Arguments $Arguments -WindowStyle $style
    if ($Interactive) { $process.WaitForExit() }
    elseif (!$process.WaitForExit(45000)) { throw "Process timeout: $($process.Id); preserve evidence." }
    $process.Refresh()
    # Persist the actual executor result before any assertion, especially an
    # unexpected ordinary interruption. Never mask it with a later audit failure.
    $runs.Add(@{Executable=$Executable;Pid=$process.Id;ExitCode=$process.ExitCode;Expected=$Expected;Utc=[DateTime]::UtcNow.ToString('O')})
    $runs | ConvertTo-Json | Set-Content (Join-Path $root 'executions.json') -Encoding UTF8
    Assert-Check ($process.ExitCode -eq $Expected) "Actual executor exit $($process.ExitCode), expected $Expected."
    return $process.ExitCode
}
function Uninstall([int]$Expected) {
    Assert-Check ((Registration) -eq $install) 'Registration does not match the owned installation.'
    $original = Join-Path $install 'Uninstall.exe'
    $executor = Join-Path $root ('executor-' + $Expected + '.exe')
    Copy-Item -LiteralPath $original -Destination $executor
    Assert-Check ((Get-FileHash $executor).Hash -eq (Get-FileHash $original).Hash) 'Copied executor differs.'
    # NSIS documented _?= suppresses the self-copy child. Copy outside the deletion
    # target so self-removal still works; never mistake the bootstrap's exit for cleanup.
    $interactive = $ReviewFailure -and $Expected -eq 24
    $options = if ($interactive) { '/CONTENT=keep _?=' } else { '/S /CONTENT=keep _?=' }
    if ($interactive) { Write-Host '仅占用自建测试文件。保留默认“保留在原收纳位置”，继续卸载；核对 wuceffectsi.dll、DeleteFileW / Windows 错误 32、卸载退出码 24 及报告位置。关闭提示后，测试脚本释放自己持有的句柄，再单独执行卸载并收尾；产品本身不会自动重试。' }
    return (Run $executor ($options + $install) $Expected -Interactive:$interactive)
}
$results = [Collections.Generic.List[object]]::new()
$runs = [Collections.Generic.List[object]]::new()
$fileLease = $null
$app = $null
try {
    Assert-Check (!(Registration)) 'This release is already installed; no changes made.'
    New-Item -ItemType Directory -Path $root | Out-Null
    $setup = Join-Path $PackageDirectory ('DesktopUp-Preview-' + $build.Release + '-win-x64-Setup.exe')
    $null = Run $setup ('/S /DESKTOPSHORTCUT=0 /D=' + $install)
    Assert-Check ((Registration) -eq $install) 'Installation registration differs.'
    $manifest = Get-Content (Join-Path $install 'app/payload-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $target = Join-Path $install 'app/wuceffectsi.dll'
    Assert-Check (Test-Path -LiteralPath $target) 'Expected WinUI payload file is missing.'
    $unknown = Join-Path $install 'unknown-user-file.txt'
    Copy-Item -LiteralPath (Join-Path $PackageDirectory 'SHA256SUMS.txt') -Destination $unknown
    $unknownHash = (Get-FileHash $unknown).Hash
    $app = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'app/DesktopUp.exe') -DataRoot $profile -Arguments '--autostart'
    $log = Join-Path $profile 'logs/desktopup.jsonl'
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        $started = (Test-Path $log) -and ((Get-Content $log -Raw -Encoding UTF8) -match '"application.started"')
        if (!$started) { Start-Sleep -Milliseconds 100 }
    } while (!$started -and !$app.HasExited -and [DateTime]::UtcNow -lt $deadline)
    Assert-Check $started 'Isolated application did not start.'
    $null = Run (Join-Path $install 'DesktopUp.exe') '--exit'
    Assert-Check ($app.WaitForExit(15000)) 'Normal application exit did not finish.'
    $db = Join-Path $profile 'state/desktopup.sqlite3'
    $dbHash = (Get-FileHash $db).Hash

    # Deliberate fault, NOT attribution of the historical interruption. Our own
    # read handle denies delete sharing until explicitly disposed below.
    $fileLease = [IO.File]::Open($target, 'Open', 'Read', 'Read')
    $exitCode = Uninstall 24
    $marker = Join-Path $install 'app/preview-install.ini'
    $failure = Get-Content $marker -Raw
    Assert-Check ($failure -match '\[LastCleanupFailure\]' -and $failure -match 'File=app\\wuceffectsi\.dll' -and $failure -match 'ExitCode=24') 'First-failure diagnosis is missing.'
    Copy-Item -LiteralPath $marker -Destination (Join-Path $root 'failed-install-marker.ini')
    Assert-Check ($failure -match 'Api=DeleteFileW' -and $failure -match 'Win32Error=32') 'Original native error missing from the marker.'
    $reports = @(Get-ChildItem (Join-Path $profile 'uninstall-reports') -Filter 'cleanup-*.ini')
    Assert-Check ($reports.Count -eq 1) 'Expected one immutable failure report.'
    $reportPath = $reports[0].FullName
    $reportText = Get-Content -LiteralPath $reportPath -Raw
    Assert-Check ($reportText -match 'Api=DeleteFileW' -and $reportText -match 'Win32Error=32' -and $reportText -match 'AfterFailure=1') 'Native failure evidence differs.'
    Assert-Check ($reportText -match ('(?m)^Pid=' + $runs[$runs.Count-1].Pid + '\s*$')) 'Report does not identify the actual uninstall executor.'
    Assert-Check ($reportText -match ('PidAndCreationFileTime=.*\b'+$PID+'@\d+;')) 'Known test handle owner missing from the post-failure snapshot.'
    $reportHash = (Get-FileHash -LiteralPath $reportPath).Hash
    Assert-Check ((Registration) -eq $install -and (Test-Path (Join-Path $install 'Uninstall.exe'))) 'Failed cleanup removed the retry entry.'
    Assert-Check (Test-Path $target) 'Locked file was removed.'
    $remaining = @($manifest | Where-Object { Test-Path -LiteralPath (Join-Path $install $_.Path) })
    $firstRemaining = $remaining[0].Path
    Assert-Check ($firstRemaining -eq 'app/wuceffectsi.dll') 'Unexpected first remaining payload.'
    $tail = $false
    foreach ($file in $manifest) {
        if ($file.Path -eq 'app/wuceffectsi.dll') { $tail = $true }
        if ($tail) { Assert-Check (Test-Path -LiteralPath (Join-Path $install $file.Path)) 'Cleanup continued after the failed file.' }
    }
    Assert-Check ((Get-FileHash $db).Hash -eq $dbHash -and (Get-FileHash $unknown).Hash -eq $unknownHash) 'Failure changed configuration or unknown content.'
    $results.Add(@{Case='controlled-delete-sharing-failure';Passed=$true;ExitCode=$exitCode;NativeError=32;Report=$reportPath;FirstRemaining=$firstRemaining;HistoricalCauseEstablished=$false})
    $fileLease.Dispose(); $fileLease = $null

    # Explicit second invocation of the original installed uninstaller; no manual
    # cleanup, process termination, delayed delete, or installer-side retry loop.
    $exitCode = Uninstall 0
    Assert-Check (!(Registration) -and !(Test-Path (Join-Path $install 'Uninstall.exe'))) 'Retry did not finish cleanup.'
    Assert-Check (@($manifest | Where-Object { Test-Path -LiteralPath (Join-Path $install $_.Path) }).Count -eq 0) 'Declared payload remains.'
    Assert-Check ((Get-FileHash $db).Hash -eq $dbHash -and (Get-FileHash $unknown).Hash -eq $unknownHash) 'Retry changed configuration or unknown content.'
    Assert-Check ((Get-FileHash $reportPath).Hash -eq $reportHash) 'Explicit retry overwrote first-failure evidence.'
    $results.Add(@{Case='explicit-retry-after-lease-release';Passed=$true;ExitCode=$exitCode;ConfigurationAndUnknownContentPreserved=$true})
    $null = Run $setup ('/S /DESKTOPSHORTCUT=0 /D=' + $install)
    $exitCode = Uninstall 0
    Assert-Check (!(Registration) -and !(Test-Path (Join-Path $install 'Uninstall.exe'))) 'Ordinary first-attempt uninstall did not finish.'
    Assert-Check ((Get-FileHash $db).Hash -eq $dbHash -and (Get-FileHash $unknown).Hash -eq $unknownHash) 'Ordinary uninstall changed configuration or unknown content.'
    $results.Add(@{Case='ordinary-uninstall-without-injected-failure';Passed=$true;ExitCode=$exitCode})
    Assert-Check ((Get-FileHash $reportPath).Hash -eq $reportHash) 'Ordinary uninstall overwrote first-failure evidence.'
    $results
} catch {
    if (Test-Path -LiteralPath $root) { $_ | Out-String | Set-Content (Join-Path $root 'failure.txt') -Encoding UTF8 }
    throw
} finally {
    if ($fileLease) { $fileLease.Dispose() }
    if ($app -and !$app.HasExited) {
        $null = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'DesktopUp.exe') -DataRoot $profile -Arguments '--exit'
        $null = $app.WaitForExit(15000)
    }
    if (Test-Path -LiteralPath $root) { $results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'results.json') -Encoding UTF8 }
    $registry.Dispose()
}
