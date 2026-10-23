#requires -Version 5.1
param([Parameter(Mandatory)][string]$OldPackage,
    [Parameter(Mandatory)][string]$NewPackage,
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Name='upgrade-auto01',
    [switch]$DefinitionsOnly,
    [switch]$UseDetectedDestination)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root=Join-Path $repo ('out/installer-upgrade/'+$Name)
$install=Join-Path $root '安装 space'
$profile=Join-Path $root 'profile'
$registry=[Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser','Registry64')
$oldBuild=Get-Content (Join-Path $OldPackage 'payload/app/preview-build.json') -Raw -Encoding UTF8|ConvertFrom-Json
$newBuild=Get-Content (Join-Path $NewPackage 'payload/app/preview-build.json') -Raw -Encoding UTF8|ConvertFrom-Json
. (Join-Path $repo 'scripts/Start-DesktopUpFixtureProcess.ps1')
$records=[Collections.Generic.List[object]]::new()
function Assert([bool]$ok,[string]$message){if(!$ok){throw $message}}
function Key([string]$release){return ('Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.'+$release)}
function Registered([string]$release){$key=$registry.OpenSubKey((Key $release));try{if($key){return $key.GetValue('InstallLocation')}}finally{if($key){$key.Dispose()}}}
function Snapshot {
    $snapshot=[ordered]@{Profiles=@();Links=@();Registrations=@();Startup=@()}
    foreach($channel in @('Preview','V3')){
        $path=Join-Path $env:LOCALAPPDATA ('DesktopUp/'+$channel)
        if(Test-Path -LiteralPath $path){$snapshot.Profiles+=@(Get-ChildItem -LiteralPath $path -File -Recurse | Sort-Object FullName | ForEach-Object {$_.FullName+'|'+(Get-FileHash -LiteralPath $_.FullName).Hash})}
    }
    foreach($folder in @('DesktopDirectory','CommonDesktopDirectory','StartMenu','CommonStartMenu')){
        $path=[Environment]::GetFolderPath($folder)
        if($path -and (Test-Path -LiteralPath $path)){$snapshot.Links+=@(Get-ChildItem -LiteralPath $path -File -Recurse -Filter '*DesktopUp*.lnk' | Sort-Object FullName | ForEach-Object {$_.FullName+'|'+(Get-FileHash -LiteralPath $_.FullName).Hash})}
    }
    $uninstall=$registry.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall')
    try{foreach($name in ($uninstall.GetSubKeyNames()|Where-Object {$_ -like 'DesktopUp*'}|Sort-Object)){$key=$uninstall.OpenSubKey($name);try{$snapshot.Registrations+=($name+'|'+$key.GetValue('InstallLocation'))}finally{$key.Dispose()}}}finally{$uninstall.Dispose()}
    foreach($path in @('Software\Microsoft\Windows\CurrentVersion\Run','Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run','Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32')){
        $key=$registry.OpenSubKey($path)
        try{if($key -and $key.GetValueNames() -contains 'DesktopUp'){$value=$key.GetValue('DesktopUp',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames);if($value -is [byte[]]){$value=[Convert]::ToBase64String($value)};$snapshot.Startup+=($path+'|'+$key.GetValueKind('DesktopUp')+'|'+$value)}}finally{if($key){$key.Dispose()}}
    }
    return $snapshot
}
function Run([string]$exe,[string]$arguments,[int]$expected=0){
    $process=Start-DesktopUpFixtureProcess -ExecutablePath $exe -DataRoot $profile -Arguments $arguments -WindowStyle Hidden
    Assert ($process.WaitForExit(60000)) ('Process timeout '+$process.Id)
    $process.Refresh()
    $records.Add(@{Exe=$exe;Arguments=$arguments;Pid=$process.Id;ExitCode=$process.ExitCode;Expected=$expected})
    $records|ConvertTo-Json -Depth 6|Set-Content (Join-Path $root 'executions.json') -Encoding UTF8
    Assert ($process.ExitCode -eq $expected) ('Exit '+$process.ExitCode+', expected '+$expected)
}
function Start-App {
    $log=Join-Path $profile 'logs/desktopup.jsonl'
    $previous=if(Test-Path -LiteralPath $log){[regex]::Matches((Get-Content $log -Raw -Encoding UTF8),'"control_center.shown"').Count}else{0}
    $null=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'DesktopUp.exe') -DataRoot $profile -WindowStyle Hidden
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    do{$app=@(Get-Process DesktopUp -ErrorAction SilentlyContinue|Where-Object {$_.Path -eq (Join-Path $install 'app/DesktopUp.exe')});if($app.Count -ne 1){Start-Sleep -Milliseconds 100}}while($app.Count -ne 1 -and [DateTime]::UtcNow -lt $deadline)
    Assert ($app.Count -eq 1) 'Isolated app did not start'
    do{$ready=(Test-Path -LiteralPath $log) -and ([regex]::Matches((Get-Content $log -Raw -Encoding UTF8),'"control_center.shown"').Count -gt $previous);if(!$ready){Start-Sleep -Milliseconds 100}}while(!$ready -and [DateTime]::UtcNow -lt $deadline)
    Assert $ready 'Control center was not shown'
    return $app[0]
}
function Exit-App($app){Run (Join-Path $install 'DesktopUp.exe') '--exit';Assert ($app.WaitForExit(15000)) 'App did not exit normally'}
function Uninstall-Owned {
    $release=if((Registered $newBuild.Release) -eq $install){$newBuild.Release}elseif((Registered $oldBuild.Release) -eq $install){$oldBuild.Release}else{return}
    $executor=Join-Path $root 'uninstall-executor.exe'
    Copy-Item -LiteralPath (Join-Path $install 'Uninstall.exe') -Destination $executor
    Run $executor ('/S /CONTENT=keep _?='+$install)
    Assert (!(Registered $release)) 'Registration remains after owned uninstall'
}
if($DefinitionsOnly){return}
Assert (!(Test-Path -LiteralPath $root)) 'Preserve old evidence; choose a new Name'
Assert (!(Get-Process DesktopUp -ErrorAction SilentlyContinue)) 'Exit DesktopUp normally first'
Assert (!(Registered $oldBuild.Release) -and !(Registered $newBuild.Release)) 'A package version is already installed; never replace it in this test'
$before=Snapshot
if($UseDetectedDestination){Assert (@($before.Registrations).Count -eq 0) 'Default-discovery test requires no existing DesktopUp registrations'}
New-Item -ItemType Directory -Path $root|Out-Null
$before|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'before.json') -Encoding UTF8
$app=$null
try{
    $oldSetup=Join-Path $OldPackage ('DesktopUp-Preview-'+$oldBuild.Release+'-win-x64-Setup.exe')
    $newSetup=Join-Path $NewPackage ('DesktopUp-Preview-'+$newBuild.Release+'-win-x64-Setup.exe')
    Run $oldSetup ('/S /DESKTOPSHORTCUT=0 /D='+$install)
    Assert ((Registered $oldBuild.Release) -eq $install) 'Old install path differs'
    $app=Start-App
    Run $newSetup ('/S /DESKTOPSHORTCUT=0 /D='+$install) 41
    Assert ((Registered $oldBuild.Release) -eq $install -and !(Registered $newBuild.Release)) 'Busy upgrade changed registration'
    Exit-App $app;$app=$null
    $db=Join-Path $profile 'state/desktopup.sqlite3'
    $dbHash=(Get-FileHash -LiteralPath $db).Hash
    $unknown=Join-Path $install 'app/unknown-owned-fixture.txt'
    Copy-Item -LiteralPath (Join-Path $OldPackage 'SHA256SUMS.txt') -Destination $unknown
    $unknownHash=(Get-FileHash -LiteralPath $unknown).Hash
    $store=$profile+'.storage'
    New-Item -ItemType Directory -Path $store -Force|Out-Null
    $content=Join-Path $store 'owned-content-fixture.txt'
    Copy-Item -LiteralPath (Join-Path $OldPackage 'SHA256SUMS.txt') -Destination $content
    $contentHash=(Get-FileHash -LiteralPath $content).Hash
    [ordered]@{Database=$dbHash;Unknown=$unknownHash;Content=$contentHash}|ConvertTo-Json|Set-Content (Join-Path $root 'pre-upgrade-hashes.json') -Encoding UTF8
    $upgradeArguments=if($UseDetectedDestination){'/S'}else{'/S /DESKTOPSHORTCUT=0 /D='+$install}
    Run $newSetup $upgradeArguments
    Assert (!(Registered $oldBuild.Release) -and (Registered $newBuild.Release) -eq $install) 'Version registration not replaced'
    Assert ((Get-FileHash -LiteralPath $db).Hash -eq $dbHash) 'Upgrade changed the database'
    Assert ((Get-FileHash -LiteralPath $unknown).Hash -eq $unknownHash -and (Get-FileHash -LiteralPath $content).Hash -eq $contentHash) 'Upgrade changed unknown/content files'
    $manifest=Get-Content (Join-Path $install 'app/payload-manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
    foreach($file in $manifest){Assert ((Get-FileHash -LiteralPath (Join-Path $install $file.Path)).Hash -eq $file.SHA256) ('Installed mismatch '+$file.Path)}
    Assert (@(Get-ChildItem -LiteralPath $install -Directory -Filter '.desktopup-upgrade-*').Count -eq 0) 'Upgrade backup remains'
    $oldMenu=Join-Path ([Environment]::GetFolderPath('Programs')) ('DesktopUp Preview '+$oldBuild.Release)
    Assert (!(Test-Path -LiteralPath (Join-Path $oldMenu 'DesktopUp Preview.lnk'))) 'Old owned menu remains'
    [ordered]@{UpgradePassed=$true;BusyRejected=$true;DatabaseHashUnchanged=$true;UnknownAndContentUnchanged=$true;InstalledHashes=@($manifest).Count;OldMenuRemoved=$true;BackupRemoved=$true}|ConvertTo-Json|Set-Content (Join-Path $root 'upgrade-verification.json') -Encoding UTF8
    $app=Start-App;Exit-App $app;$app=$null
    $events=@(Get-Content (Join-Path $profile 'logs/desktopup.jsonl') -Encoding UTF8|ForEach-Object {$_|ConvertFrom-Json})
    Assert (@($events|Where-Object {$_.eventName -eq 'application.stopped' -and $_.data.activeResources -eq 0}).Count -ge 2) 'Old/new normal shutdown not observed'
    Assert (@($events|Where-Object {$_.level -eq 'error'}).Count -eq 0) 'Application logged error'
    Uninstall-Owned
    Assert ((Get-FileHash -LiteralPath $unknown).Hash -eq $unknownHash -and (Get-FileHash -LiteralPath $content).Hash -eq $contentHash) 'Uninstall changed unknown/content files'
    [ordered]@{Passed=$true;Old=$oldBuild.Release;New=$newBuild.Release;DefaultDestinationDetected=[bool]$UseDetectedDestination;BusyRejected=$true;DataUnchanged=$true;InstalledHashes=@($manifest).Count;NormalExitAndUninstall=$true}|ConvertTo-Json|Set-Content (Join-Path $root 'results.json') -Encoding UTF8
}catch{$_|Out-String|Set-Content (Join-Path $root 'failure.txt') -Encoding UTF8;throw}
finally{
    if($app -and !$app.HasExited){Exit-App $app}
    # Preserve failures and any pending journal. Only a successful run is cleaned here.
    $after=Snapshot
    $after|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'after.json') -Encoding UTF8
    $same=($before|ConvertTo-Json -Depth 8 -Compress) -ceq ($after|ConvertTo-Json -Depth 8 -Compress)
    [ordered]@{DailyStateUnchanged=$same;RemainingProcesses=@(Get-Process DesktopUp -ErrorAction SilentlyContinue).Count}|ConvertTo-Json|Set-Content (Join-Path $root 'isolation.json') -Encoding UTF8
    $registry.Dispose()
    if(Test-Path -LiteralPath (Join-Path $root 'results.json')){Assert $same 'Daily state differs'}
}
Get-Content (Join-Path $root 'results.json')
