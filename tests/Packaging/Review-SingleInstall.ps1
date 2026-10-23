#requires -Version 5.1
param([ValidateSet('Open','Upgrade','Move','Status','Cleanup','Auto','AutoFresh')][string]$Action='Status')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')
$root=Join-Path $repo 'out/single-install/review'
$profile=Join-Path $root 'profile'
$oldPath=Join-Path $root 'installed-old'
$scope='DesktopUp.SingleInstallReview'
$regRoot='Software\Microsoft\Windows\CurrentVersion\Uninstall'
$runRoot='Software\Microsoft\Windows\CurrentVersion\Run'
$registry=[Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser','Registry64')
$legacy=Get-Content (Join-Path $root 'legacy03/fixture.json') -Raw -Encoding UTF8|ConvertFrom-Json
$current=Get-Content (Join-Path $root 'active-fixture.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($fixturePackage in @($legacy,$current)){
    if($fixturePackage.Scope -ne $scope -or $fixturePackage.Profile -ne $profile -or !$fixturePackage.Installer.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unrecognized fixture identity'}
}
$meta=Join-Path $root 'review-target.json'
if(!(Test-Path -LiteralPath $meta)){
    [ordered]@{NewPath=(Join-Path ([IO.Path]::GetTempPath()) ('DesktopUp-single-review-'+[Guid]::NewGuid().ToString('N')))}|ConvertTo-Json|Set-Content -LiteralPath $meta -Encoding UTF8
}
$newPath=(Get-Content $meta -Raw -Encoding UTF8|ConvertFrom-Json).NewPath
if([IO.Path]::GetDirectoryName($newPath) -ne [IO.Path]::GetTempPath().TrimEnd('\') -or [IO.Path]::GetFileName($newPath) -notmatch '^DesktopUp-single-review-[a-f0-9]{32}$'){throw 'Unsafe test destination'}
. (Join-Path $repo 'scripts/Start-DesktopUpFixtureProcess.ps1')
function Assert([bool]$ok,[string]$message){if(!$ok){throw $message}}
function Installed {
    $key=$registry.OpenSubKey($regRoot)
    try {
        $items=@(foreach($entry in $key.GetSubKeyNames()|Where-Object {$_ -eq $scope -or $_.StartsWith($scope+'.Preview.')}){
            $item=$key.OpenSubKey($entry)
            try{[pscustomobject]@{Key=$entry;Path=$item.GetValue('InstallLocation');Release=$item.GetValue('DisplayVersion')}}finally{$item.Dispose()}
        })
        Assert ($items.Count -le 1) 'Multiple fixture registrations; preserve evidence'
        if($items.Count){Assert ($items[0].Path -in @($oldPath,$newPath)) 'Fixture registration points outside owned targets';return $items[0]}
    }finally{$key.Dispose()}
}
function DailySnapshot {
    $values=@()
    $key=$registry.OpenSubKey($regRoot)
    try{foreach($entry in $key.GetSubKeyNames()|Where-Object {$_ -like 'DesktopUp*' -and $_ -notlike ($scope+'*')}|Sort-Object){
        $item=$key.OpenSubKey($entry)
        try{foreach($field in $item.GetValueNames()|Sort-Object){$values+=($entry+'|'+$field+'|'+$item.GetValueKind($field)+'|'+$item.GetValue($field))}}finally{$item.Dispose()}
    }}finally{$key.Dispose()}
    foreach($path in @($runRoot,'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run','Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32')){
        $item=$registry.OpenSubKey($path)
        try{if($item -and $item.GetValueNames() -contains 'DesktopUp'){$value=$item.GetValue('DesktopUp');if($value -is [byte[]]){$value=[Convert]::ToBase64String($value)};$values+=($path+'|'+$value)}}finally{if($item){$item.Dispose()}}
    }
    foreach($area in @('Preview','V3')){
        $path=Join-Path $env:LOCALAPPDATA ('DesktopUp/'+$area)
        if(Test-Path $path){$values+=@(Get-ChildItem -LiteralPath $path -File -Recurse|Sort-Object FullName|ForEach-Object {$_.FullName+'|'+(Get-FileHash -LiteralPath $_.FullName).Hash})}
    }
    foreach($area in @('DesktopDirectory','Programs')){
        $path=[Environment]::GetFolderPath($area)
        $values+=@(Get-ChildItem -LiteralPath $path -File -Recurse -Filter '*DesktopUp*.lnk'|Where-Object {$_.FullName -notlike '*DesktopUp SingleInstall Review*'}|Sort-Object FullName|ForEach-Object {$_.FullName+'|'+(Get-FileHash -LiteralPath $_.FullName).Hash})
    }
    return ($values -join "`n")
}
function Run([string]$exe,[string]$arguments,[int]$expected=0){
    $p=Start-DesktopUpFixtureProcess -ExecutablePath $exe -DataRoot $profile -Arguments $arguments -WindowStyle Hidden
    Assert ($p.WaitForExit(60000)) ('Fixture timeout '+$p.Id)
    $p.Refresh()
    Assert ($p.ExitCode -eq $expected) ('Exit '+$p.ExitCode+' expected '+$expected+' from '+$exe)
}
function Start-App {
    $entry=Installed
    Assert ([bool]$entry) 'No fixture installation'
    $appExe=Join-Path $entry.Path 'app/DesktopUp.exe'
    $log=Join-Path $profile 'logs/desktopup.jsonl'
    $previous=if(Test-Path $log){[regex]::Matches((Get-Content $log -Raw -Encoding UTF8),'control_center.shown').Count}else{0}
    $p=Start-DesktopUpFixtureProcess -ExecutablePath $appExe -DataRoot $profile -WindowStyle Hidden
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    do{
        Start-Sleep -Milliseconds 200
        $p.Refresh()
        if($p.HasExited){throw ('Fixture app exited '+$p.ExitCode)}
        $ready=(Test-Path $log) -and ([regex]::Matches((Get-Content $log -Raw -Encoding UTF8),'control_center.shown').Count -gt $previous)
    }while(!$ready -and [DateTime]::UtcNow -lt $deadline)
    Assert ([bool]$ready) 'App not ready'
    return $p
}
function Exit-App($p){$entry=Installed;Run (Join-Path $entry.Path 'DesktopUp.exe') '--exit';Assert ($p.WaitForExit(15000)) 'App did not exit normally'}
function Assert-AppLogs {
    $events=@(Get-Content (Join-Path $profile 'logs/desktopup.jsonl') -Encoding UTF8 | ForEach-Object {$_|ConvertFrom-Json})
    Assert (@($events|Where-Object {$_.level -eq 'error'}).Count -eq 0) 'Application reported errors'
    Assert (@($events|Where-Object {$_.eventName -eq 'application.stopped' -and $_.data.activeResources -eq 0}).Count -gt 0) 'Normal resource release not observed'
}
function Prepare-Old {
    foreach($package in @($legacy,$current)){Assert ((Get-FileHash -LiteralPath $package.Installer).Hash -eq $package.SHA256) 'Fixture installer changed'}
    if(!(Installed)){Run $legacy.Installer ('/S /DESKTOPSHORTCUT=0 /D='+$oldPath)}
    $samples=Join-Path $root 'samples'
    New-Item -ItemType Directory -Path $samples -Force|Out-Null
    if(!(Test-Path (Join-Path $samples '验收文档.txt'))){[IO.File]::WriteAllText((Join-Path $samples '验收文档.txt'),'Owned single-install review sample',[Text.UTF8Encoding]::new($true))}
}
function Uninstall-Fixture {
    $entry=Installed
    if(!$entry){return}
    Assert (!(Get-Process DesktopUp -ErrorAction SilentlyContinue|Where-Object {$_.Path -eq (Join-Path $entry.Path 'app/DesktopUp.exe')})) 'Exit fixture normally first'
    $executor=Join-Path $root 'uninstall-executor.exe'
    Copy-Item -LiteralPath (Join-Path $entry.Path 'Uninstall.exe') -Destination $executor -Force
    Run $executor ('/S /CONTENT=keep _?='+$entry.Path)
    Assert (!(Installed)) 'Fixture registration remains'
}
function Cleanup-Fixture {
    Uninstall-Fixture
    $archive=Join-Path $root ('closed-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $owned=@($oldPath,$newPath,$profile,($profile+'.storage'),(Join-Path $root 'samples'))
    $stream=[IO.File]::Open($archive,[IO.FileMode]::CreateNew)
    $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    $records=@()
    try {
        for($i=0;$i -lt $owned.Count;$i++){
            $folder=[IO.Path]::GetFullPath($owned[$i])
            Assert (($folder.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) -or $folder -eq $newPath) 'Cleanup outside fixture'
            if(!(Test-Path -LiteralPath $folder)){continue}
            Assert (@(Get-ChildItem -LiteralPath $folder -Recurse -Force|Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count -eq 0) 'Reparse point in fixture'
            foreach($file in Get-ChildItem -LiteralPath $folder -File -Recurse -Force){
                $entryName=$i.ToString()+'/'+$file.FullName.Substring($folder.Length+1).Replace('\','/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$entryName)|Out-Null
                $records+=[pscustomobject]@{Path=$file.FullName;Entry=$entryName;SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash}
            }
        }
    }finally{$zip.Dispose();$stream.Dispose()}
    $verify=[IO.Compression.ZipFile]::OpenRead($archive)
    try{foreach($record in $records){$entry=$verify.GetEntry($record.Entry).Open();$hash=[Security.Cryptography.SHA256]::Create();try{Assert (([BitConverter]::ToString($hash.ComputeHash($entry)).Replace('-','')) -eq $record.SHA256) 'Archive mismatch'}finally{$hash.Dispose();$entry.Dispose()}}}finally{$verify.Dispose()}
    $records|ConvertTo-Json -Depth 4|Set-Content ($archive+'.manifest.json') -Encoding UTF8
    foreach($folder in $owned){if(Test-Path -LiteralPath $folder){Remove-Item -LiteralPath $folder -Recurse -Force}}
    $state=Join-Path $env:LOCALAPPDATA 'DesktopUp/InstallerSingleReview'
    if(Test-Path $state){Assert (@(Get-ChildItem -LiteralPath $state -Force|Where-Object { $_.PSIsContainer -or $_.Name -notin @('wizard.lock','transaction.lock') -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -eq 0) 'Pending fixture state';Remove-Item -LiteralPath $state -Recurse -Force}
    Write-Host ('已卸载测试安装并清理测试文件；配置/样例可从此归档恢复：'+$archive)
}
try {
    switch($Action){
        'Status' {Installed;Write-Host "原位置：$oldPath`n迁移位置：$newPath`n独立配置：$profile"}
        'Open' {Prepare-Old;$p=Start-App;Write-Host ('已打开隔离验收版，PID '+$p.Id+'；仅使用样例目录 '+(Join-Path $root 'samples'))}
        'Upgrade' {Assert ([bool](Installed)) 'Use Open first';$p=Start-Process -FilePath $current.Installer -PassThru;Write-Host ('安装向导 PID '+$p.Id+'；默认识别测试旧位置，不会升级日常版本。')}
        'Move' {Assert ([bool](Installed)) 'Use Open / Upgrade first';$p=Start-Process -FilePath $current.Installer -ArgumentList ('/D='+$newPath) -PassThru;Write-Host ('迁移向导 PID '+$p.Id+'；请保留预填的跨盘测试位置。')}
        'Cleanup' {Cleanup-Fixture}
        'AutoFresh' {
            Assert (!(Installed)) 'Preserve existing review installation'
            $before=DailySnapshot
            $state=Join-Path $env:LOCALAPPDATA 'DesktopUp/InstallerSingleReview'
            New-Item -ItemType Directory -Path $state -Force|Out-Null
            foreach($gate in @('wizard.lock','transaction.lock')){
                $lease=[IO.File]::Open((Join-Path $state $gate),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
                try{Run $current.Installer ('/S /DESKTOPSHORTCUT=0 /D='+$oldPath) $(if($gate -eq 'wizard.lock'){31}else{41})}finally{$lease.Dispose()}
                Assert (!(Installed)) 'Concurrent installer changed registration'
            }
            Run $current.Installer ('/S /DESKTOPSHORTCUT=0 /D='+$oldPath)
            Assert ((Installed).Key -eq $scope) 'Fresh install did not use fixed identity'
            $p=Start-App;Exit-App $p;Assert-AppLogs
            Cleanup-Fixture
            Assert ($before -ceq (DailySnapshot)) 'Daily state changed'
            [ordered]@{FreshInstall=$true;ConcurrentWizardsAndTransactionsRejected=$true;FixedRegistration=$true;AppStartedAndExited=$true;Uninstalled=$true;Cleaned=$true;DailyStateUnchanged=$true}|ConvertTo-Json|Set-Content (Join-Path $root 'fresh-result.json') -Encoding UTF8
            Get-Content (Join-Path $root 'fresh-result.json')
        }
        'Auto' {
            Assert (!(Installed)) 'Preserve existing review installation'
            $before=DailySnapshot
            $before|Set-Content (Join-Path $root 'daily-before.txt') -Encoding UTF8
            Prepare-Old
            $run=$registry.CreateSubKey($runRoot)
            try{Assert ($run.GetValueNames() -notcontains $scope) 'Existing review startup';$run.SetValue($scope,('"'+$oldPath+'\DesktopUp.exe" --autostart'))}finally{$run.Dispose()}
            $p=$null
            try{
                $p=Start-App
                Run $current.Installer '/S /DESKTOPSHORTCUT=0' 41
                Assert ((Installed).Key -eq ($scope+'.Preview.'+$legacy.Release)) 'Busy upgrade changed old registration'
                Exit-App $p;$p=$null
                $db=Join-Path $profile 'state/desktopup.sqlite3';$dbHash=(Get-FileHash $db).Hash
                $unknown=Join-Path $oldPath 'app/unknown-owned-fixture.txt';[IO.File]::WriteAllText($unknown,'owned fixture, preserve')
                Run $current.Installer '/S /DESKTOPSHORTCUT=0'
                Assert ((Installed).Key -eq $scope -and (Installed).Path -eq $oldPath) 'Default in-place upgrade failed'
                Assert ((Get-FileHash $db).Hash -eq $dbHash) 'Upgrade changed database'
                Run $current.Installer ('/S /DESKTOPSHORTCUT=0 /D='+$newPath)
                Assert ((Installed).Path -eq $newPath) 'Migration failed'
                Assert (!(Test-Path (Join-Path $oldPath 'DesktopUp.exe')) -and !(Test-Path (Join-Path $oldPath 'Uninstall.exe'))) 'Old program remains'
                Assert ((Get-Content $unknown -Raw) -eq 'owned fixture, preserve') 'Unknown file modified'
                Assert ((Get-FileHash $db).Hash -eq $dbHash) 'Migration changed database'
                $run=$registry.OpenSubKey($runRoot);try{Assert ($run.GetValue($scope) -eq ('"'+$newPath+'\DesktopUp.exe" --autostart')) 'Startup path not updated'}finally{$run.Dispose()}
                $manifest=Get-Content (Join-Path $newPath 'app/payload-manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
                foreach($file in $manifest){Assert ((Get-FileHash -LiteralPath (Join-Path $newPath $file.Path)).Hash -eq $file.SHA256) ('Payload mismatch '+$file.Path)}
                Run $current.Installer ('/S /DESKTOPSHORTCUT=0 /D='+$newPath) 41
                $p=Start-App;Exit-App $p;$p=$null;Assert-AppLogs
                Cleanup-Fixture
                $after=DailySnapshot;$after|Set-Content (Join-Path $root 'daily-after.txt') -Encoding UTF8
                Assert ($before -ceq $after) 'Daily state changed'
                [ordered]@{Passed=$true;Scope='Isolated identity build; same transaction sources';DefaultDiscovery=$true;InPlace=$true;CrossVolumeRelocation=$true;OneRegistration=$true;BusyRejected=$true;SameVersionRejected=$true;DataPreserved=$true;StartupTargetUpdated=$true;InstalledHashes=@($manifest).Count;AppStartedAndExited=$true;UninstallAndCleanup=$true;DailyStateUnchanged=$true}|ConvertTo-Json|Set-Content (Join-Path $root 'auto-result.json') -Encoding UTF8
                Get-Content (Join-Path $root 'auto-result.json')
            }finally{if($p -and !$p.HasExited){Exit-App $p}}
        }
    }
}finally{$registry.Dispose()}
