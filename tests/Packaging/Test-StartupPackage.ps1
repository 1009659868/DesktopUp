#requires -Version 5.1
param([string]$PackageDirectory='out/startup/package01',[string]$EvidenceDirectory='out/startup/smoke01')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'scripts/Start-DesktopUpFixtureProcess.ps1')
function Resolve-Output([string]$path){
    if(![IO.Path]::IsPathRooted($path)){$path=Join-Path $repo $path}
    $path=[IO.Path]::GetFullPath($path).TrimEnd('\')
    if(!$path.StartsWith((Join-Path $repo 'out')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Only repository/out is allowed.'}
    return $path
}
function Await([scriptblock]$condition,[string]$label){
    $until=[DateTime]::UtcNow.AddSeconds(30)
    do {if(& $condition){return};Start-Sleep -Milliseconds 150}while([DateTime]::UtcNow -lt $until)
    throw ('Timeout: '+$label)
}
$package=Resolve-Output $PackageDirectory;$root=Resolve-Output $EvidenceDirectory
if(Test-Path -LiteralPath $root){throw 'Preserve existing evidence; choose a new directory.'}
$build=Get-Content -LiteralPath (Join-Path $package 'payload/app/preview-build.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($build.Channel -ne 'StartupReview'){throw 'Only isolated StartupReview packages are allowed.'}
$keyName='Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.'+$build.Release
$registry=[Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser','Registry64')
function Read-Install {
    $key=$registry.OpenSubKey($keyName)
    if(!$key){return $null}
    try{return $key.GetValue('InstallLocation')}finally{$key.Dispose()}
}
function Read-Startup {
    $key=$registry.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try {
        if(!$key -or !$key.GetValueNames().Contains('DesktopUp')){return 'absent'}
        return ($key.GetValueKind('DesktopUp').ToString()+':'+$key.GetValue('DesktopUp',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames))
    }finally{if($key){$key.Dispose()}}
}
if(Read-Install){$registry.Dispose();throw 'This release is already registered. No changes made.'}
$before=Read-Startup
$install=Join-Path $root 'installed space';$profile=Join-Path $root 'profile'
New-Item -ItemType Directory -Path $root | Out-Null
$setup=Join-Path $package ('DesktopUp-Preview-'+$build.Release+'-win-x64-Setup.exe')
$log=Join-Path $profile 'logs/desktopup.jsonl'
$records=@();$appProcess=$null
function Logs {
    if(!(Test-Path -LiteralPath $log)){return @()}
    return @(Get-Content -LiteralPath $log | Where-Object {$_} | ForEach-Object {$_ | ConvertFrom-Json})
}
try {
    $p=Start-DesktopUpFixtureProcess -ExecutablePath $setup -DataRoot $profile -Arguments ('/S /DESKTOPSHORTCUT=0 /D='+$install)
    if(!$p.WaitForExit(30000)){throw 'Installer did not exit.'};$p.Refresh()
    if($p.ExitCode -ne 0 -or (Read-Install) -ne $install){throw ('Install failed: '+$p.ExitCode)}
    $records += [pscustomobject]@{Step='install';Result='PASS'}
    $launcher=Join-Path $install 'DesktopUp.exe'
    $null=Start-DesktopUpFixtureProcess -ExecutablePath $launcher -DataRoot $profile -Arguments '--autostart'
    Await { @(Logs | Where-Object {$_.eventName -eq 'activation.received' -and $_.data.kind -eq 'AutoStart'}).Count -gt 0 } 'autostart dispatched'
    $first=@(Logs)[0];$appProcess=Get-Process -Id $first.processId
    $null=$appProcess.Handle # Retain a native handle before exit; Get-Process alone may lose ExitCode.
    if(!(Join-Path $install 'app/DesktopUp.exe').Equals($appProcess.Path,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected process path.'}
    if(@(Logs | Where-Object {$_.eventName -eq 'control_center.created'}).Count){throw 'AutoStart opened the center.'}
    $records += [pscustomobject]@{Step='autostart-hidden';Result='PASS';Pid=$appProcess.Id}
    $null=Start-DesktopUpFixtureProcess -ExecutablePath $launcher -DataRoot $profile -Arguments '--autostart'
    Start-Sleep -Milliseconds 1500
    if(@(Logs | Where-Object {$_.eventName -eq 'control_center.created'}).Count){throw 'Secondary AutoStart opened the center.'}
    $live=@(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $install 'app/DesktopUp.exe')})
    if($live.Count -ne 1 -or $live[0].Id -ne $appProcess.Id){throw 'Duplicate or missing app process.'}
    $records += [pscustomobject]@{Step='secondary-autostart';Result='PASS'}
    $null=Start-DesktopUpFixtureProcess -ExecutablePath $launcher -DataRoot $profile -Arguments '--show-control-center'
    Await { @(Logs | Where-Object {$_.eventName -eq 'control_center.created'}).Count -eq 1 } 'explicit center'
    $records += [pscustomobject]@{Step='explicit-center';Result='PASS'}
    $null=Start-DesktopUpFixtureProcess -ExecutablePath $launcher -DataRoot $profile -Arguments '--exit'
    if(!$appProcess.WaitForExit(15000)){throw 'App did not exit normally.'};$appProcess.Refresh()
    if($appProcess.ExitCode -ne 0){throw ('App exit: '+$appProcess.ExitCode)}
    $records += [pscustomobject]@{Step='exit';Result='PASS'}
    $p=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'Uninstall.exe') -DataRoot $profile -Arguments '/S /CONTENT=keep'
    Await { !(Read-Install) -and !(Test-Path -LiteralPath $launcher) } 'uninstall'
    if((Read-Startup) -cne $before){throw 'Unexpected system startup registration change.'}
    $records += [pscustomobject]@{Step='uninstall-startup-unchanged';Result='PASS'}
    $records | Format-Table | Out-Host
}finally{
    $records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding UTF8
    $registry.Dispose()
}
