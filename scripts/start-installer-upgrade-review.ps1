#requires -Version 5.1
param([ValidateSet('Open','Upgrade','Status','Cleanup')][string]$Action='Open')
$ErrorActionPreference='Stop'
$reviewRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$reviewOld=Join-Path $reviewRepo 'out/releases/main-poster-fixes-final01'
$reviewNew=Join-Path $reviewRepo 'out/installer-upgrade/candidate05'
. (Join-Path $reviewRepo 'tests/Packaging/Test-InstallerUpgrade.ps1') -OldPackage $reviewOld -NewPackage $reviewNew -Name 'upgrade-review01' -DefinitionsOnly
$marker=Join-Path $root 'review-owner.json'
function Check-Review {
    Assert (Test-Path -LiteralPath $marker) '验收目录缺少归属记录；未操作。'
    $owner=Get-Content -LiteralPath $marker -Raw -Encoding UTF8|ConvertFrom-Json
    Assert ($owner.Root -eq $root -and $owner.Install -eq $install -and $owner.Profile -eq $profile) '验收目录身份不一致；未操作。'
    Assert ($owner.Old -eq $oldBuild.Release -and $owner.New -eq $newBuild.Release) '验收包版本已变化；未覆盖旧数据。'
    foreach($version in @($oldBuild.Release,$newBuild.Release)){$location=Registered $version;Assert (!$location -or $location -eq $install) '该版本登记在其他目录；未操作。'}
}
try{
    if(!(Test-Path -LiteralPath $root)){
        if($Action -ne 'Open'){throw '请先使用 -Action Open 创建隔离验收。'}
        Assert (!(Get-Process DesktopUp -ErrorAction SilentlyContinue)) '请先从托盘正常退出正在运行的 DesktopUp。'
        Assert (!(Registered $oldBuild.Release) -and !(Registered $newBuild.Release)) '验收包版本已在其他位置安装，未覆盖。'
        New-Item -ItemType Directory -Path $root|Out-Null
        Snapshot|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'before.json') -Encoding UTF8
        [ordered]@{Root=$root;Install=$install;Profile=$profile;Old=$oldBuild.Release;New=$newBuild.Release}|ConvertTo-Json|Set-Content -LiteralPath $marker -Encoding UTF8
        Run (Join-Path $OldPackage ('DesktopUp-Preview-'+$oldBuild.Release+'-win-x64-Setup.exe')) ('/S /DESKTOPSHORTCUT=0 /D='+$install)
        $samples=Join-Path $root 'samples';New-Item -ItemType Directory -Path $samples|Out-Null
        foreach($sampleName in @('升级验收-A.txt','升级验收-B.txt')){[IO.File]::WriteAllText((Join-Path $samples $sampleName),'DesktopUp owned upgrade review sample: '+$sampleName,[Text.UTF8Encoding]::new($false))}
    }
    Check-Review
    if($Action -eq 'Status'){
        [pscustomobject]@{Install=$install;OldRegistered=[bool](Registered $oldBuild.Release);NewRegistered=[bool](Registered $newBuild.Release);Profile=$profile;Samples=(Join-Path $root 'samples');Running=@(Get-Process DesktopUp -ErrorAction SilentlyContinue).Count}
        return
    }
    if($Action -eq 'Open'){
        Assert ((Registered $oldBuild.Release) -or (Registered $newBuild.Release)) '测试程序已卸载；配置保留。请先 Cleanup 收尾后再创建新验收。'
        $other=@(Get-Process DesktopUp -ErrorAction SilentlyContinue|Where-Object {$_.Path -ne (Join-Path $install 'app/DesktopUp.exe')})
        Assert ($other.Count -eq 0) '另一安装的 DesktopUp 正在运行；未转发到它。'
        $process=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'DesktopUp.exe') -DataRoot $profile -Arguments '--show-control-center' -WindowStyle Normal
        Write-Host "已打开隔离验收。安装位置：$install"
        Write-Host "可在旧版创建海报，并拖入这里的两个自建样例：$(Join-Path $root 'samples')"
        Write-Host '准备好后从托盘退出，再运行本脚本 -Action Upgrade。升级完成后 -Action Open 复验。'
        return
    }
    Assert (!(Get-Process DesktopUp -ErrorAction SilentlyContinue)) '请先从托盘正常退出 DesktopUp；不会强制结束进程。'
    if($Action -eq 'Upgrade'){
        Assert ((Registered $oldBuild.Release) -eq $install -and !(Registered $newBuild.Release)) '当前不是待升级的旧版验收安装。'
        $process=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $NewPackage ('DesktopUp-Preview-'+$newBuild.Release+'-win-x64-Setup.exe')) -DataRoot $profile -Arguments ('/D='+$install) -WindowStyle Normal
        Write-Host "已打开新版安装向导，PID $($process.Id)。请保持预选的隔离安装目录，不选择日常安装目录。"
        Write-Host '取消安装不改变旧版。安装完成页无需立即启动，关闭向导后使用 -Action Open 保持隔离配置。'
        return
    }
    Assert (@(Get-ChildItem -LiteralPath $install -Directory -Filter '.desktopup-upgrade-*' -ErrorAction SilentlyContinue).Count -eq 0) '存在待恢复升级，请先保留现场并反馈，不做清理。'
    Uninstall-Owned
    Assert (!(Test-Path -LiteralPath (Join-Path $install 'DesktopUp.exe')) -and !(Test-Path -LiteralPath (Join-Path $install 'Uninstall.exe'))) '程序清理未完成；保留现场。'
    $after=Snapshot
    $after|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'after.json') -Encoding UTF8
    $before=Get-Content (Join-Path $root 'before.json') -Raw -Encoding UTF8|ConvertFrom-Json
    Assert (($before|ConvertTo-Json -Depth 8 -Compress) -ceq ($after|ConvertTo-Json -Depth 8 -Compress)) '日常状态与验收前不同，保留证据，请反馈。'
    $resolved=(Resolve-Path -LiteralPath $root).Path
    Assert ($resolved -eq (Join-Path $reviewRepo 'out\installer-upgrade\upgrade-review01')) '清理范围不一致。'
    $entries=@(Get-Item -LiteralPath $root)+@(Get-ChildItem -LiteralPath $root -Recurse -Force)
    Assert (@($entries|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -eq 0) '验收目录含联接；不递归清理。'
    $files=@($entries|Where-Object {!$_.PSIsContainer}|ForEach-Object {[pscustomobject]@{Path=$_.FullName;Relative=$_.FullName.Substring($root.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
    $archive=Join-Path (Split-Path -Parent $root) ('upgrade-review-closed-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
    $zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($file in $files){$null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.Path,$file.Relative)}}finally{$zip.Dispose()}
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try{
        foreach($file in $files){$stream=$zip.GetEntry($file.Relative).Open();$sha=[Security.Cryptography.SHA256]::Create();try{$digest=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()};Assert ($digest -eq $file.SHA256 -and (Get-FileHash -LiteralPath $file.Path).Hash -eq $file.SHA256) '归档校验不一致；未删除原件。'}
    }finally{$zip.Dispose()}
    Assert (@(Get-ChildItem -LiteralPath $root -File -Recurse -Force).Count -eq $files.Count) '归档期间文件发生变化；未删除原件。'
    Remove-Item -LiteralPath $resolved -Recurse -Force
    Write-Host "测试安装和入口已清理；配置、收纳样例及记录可从此归档恢复：$archive"
}finally{$registry.Dispose()}
