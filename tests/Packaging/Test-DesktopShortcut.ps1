#requires -Version 5.1
param([string]$OutputRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputRoot) { $OutputRoot = Join-Path $repo ('out/installer-shortcut-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!$OutputRoot.StartsWith((Join-Path $repo 'out') + '\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputRoot)) {
    throw 'Choose a new evidence directory below repository/out.'
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
. (Join-Path $repo 'scripts/toolchain.ps1')
$shortcutTools = Enter-DesktopUpToolchain -ForPackaging
$release = '20991014.' + (Get-Date -Format 'HHmmss')
$product = 'DesktopUp Preview ' + $release
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$linkPath = Join-Path $desktop ($product + '.lnk')
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.' + $release
$fixture = Join-Path $PSScriptRoot 'Fixtures/installer-payload.txt'
$results = [Collections.Generic.List[object]]::new()
function Assert-Check([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Read-LinkTarget([string]$Path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $null
    try { $link = $shell.CreateShortcut($Path); return $link.TargetPath }
    finally { if ($link) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) }; [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}
function Write-OwnedLink([string]$Target) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $null
    try { $link = $shell.CreateShortcut($linkPath); $link.TargetPath = $Target; $link.Save() }
    finally { if ($link) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) }; [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}
function Uninstall-Owned([string]$Directory) {
    $registered = (Get-ItemProperty -LiteralPath $registry).InstallLocation
    Assert-Check ($registered -eq $Directory) 'Unexpected uninstall registry location; refuse cleanup.'
    . (Join-Path $repo 'scripts/Start-DesktopUpFixtureProcess.ps1')
    $process = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $Directory 'Uninstall.exe') -DataRoot (Join-Path $OutputRoot 'profile') -Arguments '/S' -WindowStyle Hidden
    if(!$process.WaitForExit(15000)){throw 'Fixture uninstall did not exit.'}
    Assert-Check ($process.ExitCode -eq 0) 'Fixture uninstaller failed.'
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while((Test-Path -LiteralPath $registry) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
    Assert-Check (!(Test-Path -LiteralPath $registry)) 'Uninstall registration remains.'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $Directory 'DesktopUp.exe'))) 'Owned fixture payload remains.'
}
try {
    Assert-Check (!(Test-Path -LiteralPath $linkPath)) 'Reserved test shortcut already exists; no files changed.'
    Assert-Check (!(Test-Path -LiteralPath $registry)) 'Reserved test release already registered; no install performed.'
    $source = Join-Path $repo 'packaging/preview/installer.nsi'
    $script = Get-Content -LiteralPath $source -Raw
    Assert-Check ($script.Contains('Page custom InstallOptionsCreate InstallOptionsLeave')) 'Options page not wired.'
    Assert-Check ($script.Contains('${NSD_OnClick} $DesktopShortcutCheckbox InstallOptionsChanged')) 'Back/Next state not tracked.'
    $installList = Join-Path $OutputRoot 'install-files.nsh'
    $uninstallList = Join-Path $OutputRoot 'uninstall-files.nsh'
    $guards = Join-Path $OutputRoot 'uninstall-guards.nsh'
    $bom = New-Object Text.UTF8Encoding($true)
    $helper=Join-Path $OutputRoot 'uninstall-helper.exe'
    & $shortcutTools.MakeNsis /V2 /WX "/DFIXTURE_OUTPUT=$helper" (Join-Path $PSScriptRoot 'Fixtures/uninstall-helper.nsi')
    Assert-Check ($LASTEXITCODE -eq 0) 'Helper compile failed.'
    [IO.File]::WriteAllLines($installList, @('SetOutPath "$INSTDIR\app"', ('File /oname=DesktopUp.exe "' + $helper + '"'), 'SetOutPath "$INSTDIR"', ('File /oname=DesktopUp.exe "' + $fixture + '"'), ('File /oname=PREVIEW-README.txt "' + $fixture + '"')), $bom)
    [IO.File]::WriteAllLines($uninstallList, @('Delete "$INSTDIR\DesktopUp.exe"', 'IfErrors uninstall_failed', 'Delete "$INSTDIR\app\DesktopUp.exe"', 'IfErrors uninstall_failed', 'Delete "$INSTDIR\PREVIEW-README.txt"', 'IfErrors uninstall_failed'), $bom)
    [IO.File]::WriteAllLines($guards, @('StrCpy $GuardPath "$INSTDIR\app"', 'Call un.RequireSafeDirectory'), $bom)
    $setup = Join-Path $OutputRoot 'installer-fixture.exe'
    & $shortcutTools.MakeNsis /V3 /WX /INPUTCHARSET UTF8 "/DRELEASE_ID=$release" /DFILE_VERSION=0.0.0.1 "/DSETUP_OUTPUT=$setup" "/DINSTALL_FILES=$installList" "/DUNINSTALL_FILES=$uninstallList" "/DUNINSTALL_GUARDS=$guards" /DMAX_INSTALL_PATH=180 /DPAYLOAD_KB=1 $source 2>&1 |
        Tee-Object -FilePath (Join-Path $OutputRoot 'compile.log') | Out-Host
    Assert-Check ($LASTEXITCODE -eq 0) 'NSIS compile failed.'
    foreach ($scenario in @('default-on', 'off', 'collision', 'retargeted')) {
        $destination = Join-Path $OutputRoot ('安装 目录 ' + $scenario)
        $sentinelHash = $null
        if ($scenario -eq 'collision') {
            Write-OwnedLink $fixture
            $sentinelHash = (Get-FileHash -LiteralPath $linkPath).Hash
        }
        $flag = if ($scenario -eq 'off') { '/DESKTOPSHORTCUT=0' } elseif ($scenario -eq 'retargeted') { '/DESKTOPSHORTCUT=1' } else { '' }
        $process = Start-Process -FilePath $setup -ArgumentList ("/S $flag /D=$destination") -WindowStyle Hidden -PassThru -Wait
        Assert-Check ($process.ExitCode -eq 0) "Install failed: $scenario ($($process.ExitCode))"
        $marker = Get-Content -LiteralPath (Join-Path $destination 'app/preview-install.ini') -Raw
        if ($scenario -eq 'off') {
            Assert-Check (!(Test-Path -LiteralPath $linkPath)) 'Unchecked option still created a desktop link.'
            Assert-Check (!$marker.Contains('DesktopShortcutCreated=1')) 'Unchecked option claimed a desktop link.'
        } elseif ($scenario -eq 'collision') {
            Assert-Check ((Get-FileHash -LiteralPath $linkPath).Hash -eq $sentinelHash) 'Existing shortcut was overwritten.'
            Assert-Check (!$marker.Contains('DesktopShortcutCreated=1')) 'Existing shortcut was claimed.'
        } else {
            Assert-Check ((Read-LinkTarget $linkPath) -eq (Join-Path $destination 'DesktopUp.exe')) 'Shortcut target is not the installed launcher.'
            Assert-Check ($marker.Contains('DesktopShortcutCreated=1')) 'Created shortcut was not recorded.'
            if ($scenario -eq 'retargeted') {
                Write-OwnedLink $fixture
                $sentinelHash = (Get-FileHash -LiteralPath $linkPath).Hash
            }
        }
        Uninstall-Owned $destination
        if ($sentinelHash) {
            Assert-Check ((Get-FileHash -LiteralPath $linkPath).Hash -eq $sentinelHash) 'Uninstaller removed/modified an unrelated shortcut.'
            # This exact link was created by this fixture, not a user shortcut.
            Remove-Item -LiteralPath $linkPath
        } else { Assert-Check (!(Test-Path -LiteralPath $linkPath)) 'Owned desktop shortcut was not cleaned.' }
        $results.Add([ordered]@{ Scenario=$scenario; Passed=$true; InstallDirectory=$destination })
    }
    $badPath = Join-Path $OutputRoot 'invalid-option'
    $bad = Start-Process -FilePath $setup -ArgumentList ("/S /DESKTOPSHORTCUT=2 /D=$badPath") -WindowStyle Hidden -PassThru -Wait
    Assert-Check ($bad.ExitCode -eq 17 -and !(Test-Path -LiteralPath $badPath) -and !(Test-Path -LiteralPath $registry)) 'Invalid option did not fail before installation.'
    $results.Add([ordered]@{ Scenario='invalid-option'; Passed=$true })
    $results | ConvertTo-Json -Depth 4 | Tee-Object -FilePath (Join-Path $OutputRoot 'results.json')
    Write-Host 'Fixture installs and owned desktop/start-menu entries cleaned; no DesktopUp app or user data was used.'
} finally {
    # On failure preserve evidence and any owned install for diagnosis; never broad-delete.
    Exit-DesktopUpToolchain $shortcutTools
}
