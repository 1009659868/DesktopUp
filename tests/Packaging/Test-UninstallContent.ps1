#requires -Version 5.1
param([Parameter(Mandatory)][string]$PackageDirectory,[Parameter(Mandatory)][string]$FixtureDirectory,[string]$Name='uninstall-package01')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if($Name -notmatch '^[a-zA-Z0-9-]+$'){throw 'Simple owned name required.'}
$root=Join-Path $repo ('out/drop-d3/'+$Name)
if(Test-Path -LiteralPath $root){throw 'Existing evidence preserved.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first.'}
$settings=Get-Content -LiteralPath (Join-Path $PackageDirectory 'evidence/resolved-settings.json') -Raw | ConvertFrom-Json
$registry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.'+$settings.ReleaseId
if(Test-Path -LiteralPath $registry){throw 'Release already installed; no change.'}
$setup=Join-Path $PackageDirectory ("DesktopUp-Preview-$($settings.ReleaseId)-win-x64-Setup.exe")
$install=Join-Path $root 'installed'
$profile=Join-Path $root 'profile'
. (Join-Path $repo 'scripts/toolchain.ps1')
. (Join-Path $repo 'scripts/Start-DesktopUpFixtureProcess.ps1')
$pkgTools=Enter-DesktopUpToolchain -Offline
$results=[Collections.Generic.List[object]]::new()
function Assert-Check([bool]$Condition,[string]$Message){if(!$Condition){throw $Message}}
function Run([string]$Executable,[string]$Arguments,[string]$Data=$profile,[int]$Expected=0){
    $p=Start-DesktopUpFixtureProcess -ExecutablePath $Executable -DataRoot $Data -Arguments $Arguments
    if(!$p.WaitForExit(60000)){throw "Process still running: $($p.Id). Evidence preserved."}
    $p.Refresh();Assert-Check ($p.ExitCode -eq $Expected) "Unexpected exit $($p.ExitCode): $Arguments"
}
function Install {
    Run $setup ("/S /DESKTOPSHORTCUT=0 /D=$install")
    Assert-Check ((Get-ItemProperty -LiteralPath $registry).InstallLocation -eq $install) 'Registration differs.'
    Assert-Check (Test-Path -LiteralPath (Join-Path $install 'app/DesktopUp.exe')) 'Installed app missing.'
}
function UninstallKeep([string]$Data=$profile){
    Run (Join-Path $install 'Uninstall.exe') '/S /CONTENT=keep' $Data
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while((Test-Path -LiteralPath $registry) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
    Assert-Check (!(Test-Path -LiteralPath $registry)) 'Uninstall did not remove registration.'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $install 'DesktopUp.exe'))) 'Package remains.'
    Assert-Check ((Get-Content -LiteralPath (Join-Path $install 'user-sentinel.txt') -Raw) -eq 'owned sentinel') 'Unknown file changed.'
}
try {
    New-Item -ItemType Directory -Path $install | Out-Null
    [IO.File]::WriteAllText((Join-Path $install 'user-sentinel.txt'),'owned sentinel')
    Install
    UninstallKeep (Join-Path $root 'never-started')
    $results.Add(@{Case='never-started-and-exact-cleanup';Passed=$true})
    [IO.File]::WriteAllText((Join-Path $root '.uninstall-fixture'),'owned')
    & (Join-Path $FixtureDirectory 'DesktopUp.SessionFixture.exe') $profile --uninstall-content
    if($LASTEXITCODE){throw 'Fixture failed.'}
    Install
    UninstallKeep
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $root 'original/return-file.txt'))) 'Keep moved original.'
    Assert-Check (Test-Path -LiteralPath (Join-Path $profile 'state/desktopup.sqlite3')) 'Keep removed configuration.'
    $results.Add(@{Case='populated-keep';Passed=$true})
    Install
    $before=@(Get-ChildItem -LiteralPath (Join-Path $profile 'uninstall-reports') -File).Count
    Run (Join-Path $install 'Uninstall.exe') '/S /CONTENT=return'
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while(@(Get-ChildItem -LiteralPath (Join-Path $profile 'uninstall-reports') -File).Count -eq $before -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
    Assert-Check (@(Get-ChildItem -LiteralPath (Join-Path $profile 'uninstall-reports') -File).Count -gt $before) 'Failure report missing.'
    Assert-Check (Test-Path -LiteralPath $registry) 'Refused return unexpectedly uninstalled.'
    Assert-Check (Test-Path -LiteralPath (Join-Path $install 'app/DesktopUp.exe')) 'Refused return deleted program.'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $root 'original/return-file.txt'))) 'Silent return moved file.'
    # Wait for the temporary uninstaller to release the profile lease after its failure report.
    $lock=Join-Path $profile 'uninstall-session.lock';$lease=$null
    do {try{$lease=[IO.File]::Open($lock,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{Start-Sleep -Milliseconds 100}}while(!$lease -and [DateTime]::UtcNow -lt $deadline)
    Assert-Check ($null -ne $lease) 'Uninstall lease not released.';$lease.Dispose()
    $results.Add(@{Case='reinstall-and-refused-return-keeps-program';Passed=$true})
    Run (Join-Path $install 'app/DesktopUp.exe') '--prepare-uninstall=return'
    Assert-Check (Test-Path -LiteralPath (Join-Path $root 'original/return-file.txt')) 'File did not return.'
    Assert-Check (Test-Path -LiteralPath (Join-Path $root 'original/return-folder/child.txt')) 'Folder did not return.'
    UninstallKeep
    $results.Add(@{Case='installed-helper-return-and-cleanup';Passed=$true})
    $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding UTF8
    $results
} finally {Exit-DesktopUpToolchain $pkgTools}
