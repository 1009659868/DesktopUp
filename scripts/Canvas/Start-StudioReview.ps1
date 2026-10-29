#requires -Version 5.1
param([ValidatePattern('^canvas-studio-review[0-9]{2}$')][string]$Name = 'canvas-studio-review07')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixtureRoot = Join-Path $repoRoot ('out\drop-d3\' + $Name)
$profileRoot = Join-Path $fixtureRoot 'profile'
$program = Join-Path $repoRoot 'out\canvas-script-editing-20261029\candidate\DesktopUp.exe'
if (!(Test-Path -LiteralPath $program)) { throw 'Build the J1 studio candidate before running this review entry.' }
if (Get-Process -Name DesktopUp -ErrorAction SilentlyContinue) { throw 'Please exit the running DesktopUp normally from its tray first.' }
. (Join-Path $repoRoot 'scripts\toolchain.ps1')
. (Join-Path $repoRoot 'scripts\Start-DesktopUpFixtureProcess.ps1')
. (Join-Path $PSScriptRoot 'Get-StudioLaunchPermission.ps1')
$studioTools = Enter-DesktopUpToolchain -Offline
try {
    if (!(Test-Path -LiteralPath $fixtureRoot)) {
        New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
        [IO.File]::WriteAllText((Join-Path $fixtureRoot '.canvas-fixture'), 'Owned J1 studio human review')
        [IO.File]::WriteAllText((Join-Path $fixtureRoot '.uninstall-fixture'), 'Owned J1 studio human review')
    }
    if (!(Test-Path -LiteralPath (Join-Path $fixtureRoot '.canvas-fixture')) -or !(Test-Path -LiteralPath (Join-Path $fixtureRoot '.uninstall-fixture'))) { throw 'Unmarked directory; nothing was changed.' }
    if (!(Test-Path -LiteralPath $profileRoot)) {
        & $studioTools.DotNet run --project (Join-Path $repoRoot 'tests\DesktopUp.SessionFixture\DesktopUp.SessionFixture.csproj') -c Release '-p:Platform=x64' '-p:NuGetAudit=false' "-p:RestoreConfigFile=$($studioTools.OfflineConfig)" -- $profileRoot --canvas-kernel
        if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture generation failed.' }
    }
    $candidate = Start-DesktopUpFixtureProcess -ExecutablePath $program -DataRoot $profileRoot -Arguments '--show-control-center' -WindowStyle Hidden
    Write-Host "J1 studio candidate PID: $($candidate.Id); isolated profile: $profileRoot"
    $permission = Get-StudioLaunchPermission -ProcessId $candidate.Id
    if ($permission.Status -eq 'Mismatch') {
        Write-Warning ("DesktopUp 与桌面资源管理器的运行权限不一致（{0}），拖入可能受限。程序已启动；脚本没有修改系统设置或进程权限。" -f $permission.Reason)
    } elseif ($permission.Status -eq 'Unknown') {
        Write-Verbose ("Drag permission comparison unavailable: " + $permission.Reason)
    }
} finally { Exit-DesktopUpToolchain $studioTools }
