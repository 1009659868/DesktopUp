#requires -Version 5.1
# ASCII so both Windows PowerShell 5.1 and PowerShell 7 parse this entry point.
param([ValidateSet('Open','Install','Status','Uninstall')][string]$Action='Open')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$release='20261016.903'
$setup=Join-Path $repo "out/startup/package02/DesktopUp-Preview-$release-win-x64-Setup.exe"
$suggestedInstall=Join-Path $repo 'out/startup/review01/installed space'
$keyName='Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.'+$release
$profile=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DesktopUp/StartupReview'
function Get-ReviewInstall {
    $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser','Registry64')
    try {
        $key=$base.OpenSubKey($keyName)
        if(!$key){return $null}
        try {$path=[string]$key.GetValue('InstallLocation')}finally{$key.Dispose()}
        if(!$path){throw 'Registered installation has no location. No changes made.'}
        $buildFile=Join-Path $path 'app/preview-build.json'
        if(!(Test-Path -LiteralPath $buildFile)){throw 'Registered package is incomplete. No changes made.'}
        $build=Get-Content -LiteralPath $buildFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if($build.Channel -ne 'StartupReview' -or $build.Release -ne $release){throw 'Not this isolated review package. No changes made.'}
        return $path
    } finally {$base.Dispose()}
}
function Start-ReviewProcess([string]$exe,[string]$arguments) {
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$exe;$start.Arguments=$arguments;$start.UseShellExecute=$false
    $start.WorkingDirectory=Split-Path -Parent $exe
    $start.CreateNoWindow=$true
    foreach($name in @('DESKTOPUP_V3_TEST_DATA_ROOT','DESKTOPUP_V3_TEST_STORAGE_BASE','DESKTOPUP_V3_MENU_DIAGNOSTICS','DESKTOPUP_TEST_CONTENT_FEEDBACK')){
        $start.EnvironmentVariables.Remove($name)
    }
    return [Diagnostics.Process]::Start($start)
}
$install=Get-ReviewInstall
if($Action -eq 'Status'){
    [pscustomobject]@{Release=$release;Installed=[bool]$install;InstallLocation=$install;Profile=$profile;SetupExists=(Test-Path -LiteralPath $setup)}
    return
}
if(!$install){
    if($Action -eq 'Uninstall'){Write-Host 'This review is not installed.';return}
    if(!(Test-Path -LiteralPath $setup)){throw 'Build the StartupReview package first.'}
    # Explicit, visible installer: no silent install, UAC bypass, or desktop shortcut by default.
    $p=Start-ReviewProcess $setup ('/DESKTOPSHORTCUT=0 /D='+$suggestedInstall)
    Write-Host ('Installer opened (PID '+$p.Id+'). Complete installation, then run this script again with -Action Open.')
    return
}
if($Action -eq 'Install'){Write-Host ('Already installed: '+$install);return}
if($Action -eq 'Uninstall'){
    $p=Start-ReviewProcess (Join-Path $install 'Uninstall.exe') '/CONTENT=keep'
    Write-Host ('Uninstaller opened (PID '+$p.Id+'). Review configuration is retained; no file return tests are needed.')
}else{
    $p=Start-ReviewProcess (Join-Path $install 'DesktopUp.exe') '--show-control-center'
    Write-Host ('Opened isolated startup review (launcher PID '+$p.Id+'). Profile: '+$profile)
    Write-Host 'Startup registration is OFF by default. Enable it yourself in Control Center > Global.'
}
