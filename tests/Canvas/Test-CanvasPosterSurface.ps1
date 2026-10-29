#requires -Version 5.1
param([ValidatePattern('^canvas-surface-auto[0-9]{2}$')][string]$Name = 'canvas-surface-auto01',
    [string]$ProgramDirectory = 'out\canvas-poster-surface-20261029\review', [switch]$CurrentPoster, [switch]$ContextTools, [switch]$TitleDiagnostic)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root = Join-Path $repo ('out\drop-d3\' + $Name)
$profile = Join-Path $root 'profile'
if (Test-Path -LiteralPath $root) { throw 'Use a fresh, independently owned fixture name.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit the existing DesktopUp normally first.' }
$program = Join-Path (Join-Path $repo $ProgramDirectory) 'DesktopUp.exe'
foreach ($project in @('App', 'Domain', 'Application', 'Infrastructure', 'Windows')) {
    $assemblyName = if ($project -eq 'App') { 'DesktopUp.dll' } else { 'DesktopUp.' + $project + '.dll' }
    $assembly = Get-Item -LiteralPath (Join-Path (Split-Path -Parent $program) $assemblyName)
    $sourceRoots = @((Join-Path $repo ('src\DesktopUp.' + $project)))
    if ($project -eq 'App') { $sourceRoots += Join-Path $repo 'tools\review\App' }
    $newerSource = Get-ChildItem -LiteralPath $sourceRoots -File -Recurse |
        Where-Object { $_.Extension -in '.cs', '.xaml' -and $_.FullName -notmatch '\\(obj|bin)\\' -and $_.LastWriteTimeUtc -gt $assembly.LastWriteTimeUtc } | Select-Object -First 1
    if ($newerSource) { throw ('Source is newer than its assembly; rebuild first: ' + $newerSource.FullName) }
}
. (Join-Path $repo 'scripts\toolchain.ps1')
. (Join-Path $repo 'scripts\Start-DesktopUpFixtureProcess.ps1')
$surfaceTools = Enter-DesktopUpToolchain -Offline
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    [IO.File]::WriteAllText((Join-Path $root '.canvas-fixture'), 'Owned canvas surface automated fixture')
    [IO.File]::WriteAllText((Join-Path $root '.uninstall-fixture'), 'Owned canvas surface automated fixture')
    & $surfaceTools.DotNet run --project (Join-Path $repo 'tests\DesktopUp.SessionFixture\DesktopUp.SessionFixture.csproj') -c Release '-p:Platform=x64' '-p:NuGetAudit=false' "-p:RestoreConfigFile=$($surfaceTools.OfflineConfig)" -- $profile --canvas-kernel
    if ($LASTEXITCODE -ne 0) { throw 'Fixture creation failed.' }
    $contentRoot = Join-Path $root 'storage'
    function Read-OwnedContentFingerprint {
        @(Get-ChildItem -LiteralPath $contentRoot -File -Recurse | Where-Object { !$_.Name.StartsWith('.') } | Sort-Object FullName | ForEach-Object {
            $_.FullName.Substring($contentRoot.Length) + '|' + $_.Length + '|' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }) -join "`n"
    }
    $beforeContent = Read-OwnedContentFingerprint
    $arguments = if ($TitleDiagnostic) { '--canvas-kernel-probe --canvas-title-diagnostic' } elseif ($ContextTools) { '--canvas-kernel-probe --canvas-context-probe' } elseif ($CurrentPoster) { '--canvas-kernel-probe --canvas-current-probe' } else { '--canvas-kernel-probe' }
    $process = Start-DesktopUpFixtureProcess -ExecutablePath $program -DataRoot $profile -Arguments $arguments -WindowStyle Hidden
    Write-Host "Isolated native probe PID $($process.Id); $root"
    if (!$process.WaitForExit(55000)) { throw 'Probe still active; retained its owned fixture, no forced termination.' }
    $records = @(Get-Content -LiteralPath (Join-Path $profile 'logs\desktopup.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $result = $records | Where-Object eventName -eq 'canvas.kernel_observed' | Select-Object -Last 1
    $stop = $records | Where-Object eventName -eq 'application.stopped' | Select-Object -Last 1
    $result | ConvertTo-Json -Depth 8
    if (!$result.data.passed -or $null -eq $stop -or $stop.data.activeResources -ne 0) { throw 'Native probe failed or resource ledger did not close; fixture retained for diagnosis.' }
    if ((Read-OwnedContentFingerprint) -cne $beforeContent) { throw 'Owned real content changed; fixture retained.' }
    [IO.File]::WriteAllText((Join-Path $profile 'content-fingerprint.txt'), $beforeContent)
    Write-Host 'Owned real-content paths, lengths and SHA256 unchanged; active resources: 0.'
} finally { Exit-DesktopUpToolchain $surfaceTools }
