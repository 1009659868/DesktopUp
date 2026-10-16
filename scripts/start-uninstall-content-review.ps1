#requires -Version 5.1
# Keep the UTF-8 BOM: Windows PowerShell 5.1 otherwise reads Chinese text as ANSI.
param([ValidateSet('Open','Uninstall','Folder','Status')][string]$Action='Open')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$release='20261015.12'
$package=Join-Path $repo 'out/uninstall-content/package01'
$setup=Join-Path $package ("DesktopUp-Preview-$release-win-x64-Setup.exe")
$root=Join-Path $repo 'out/drop-d3/uninstall-review01'
$profile=Join-Path $root 'profile'
# The previous tool-installed copy stays untouched. Install this copy from the
# human's terminal so its HKCU registration and later uninstall share a context.
$install=Join-Path $root 'installed-user'
$registryKey='Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.'+$release
function Get-ReviewInstallation {
    # Match NSIS SetRegView 64 explicitly; do not depend on the caller's HKCU PSDrive.
    $base=$null;$key=$null;$registered=$false;$registeredPath=$null
    try {
        $base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
        $key=$base.OpenSubKey($registryKey)
        $registered=$null -ne $key
        if($registered){$registeredPath=[string]$key.GetValue('InstallLocation')}
    } finally {if($key){$key.Dispose()};if($base){$base.Dispose()}}
    $marker=Join-Path $install 'app/preview-install.ini'
    $fields=@{};$section='';$duplicate=$false
    if([IO.File]::Exists($marker)){
        foreach($line in [IO.File]::ReadAllLines($marker)){
            $line=$line.Trim()
            if($line -match '^\[([^\]]+)\]$'){$section=$matches[1];continue}
            if($section -eq 'Install' -and $line -match '^([^=]+)=(.*)$'){
                $field=$matches[1].Trim();if($fields.ContainsKey($field)){$duplicate=$true};$fields[$field]=$matches[2].Trim()
            }
        }
    }
    $pathMatches=$false
    if(![string]::IsNullOrWhiteSpace($registeredPath)){
        try {$pathMatches=[IO.Path]::GetFullPath($registeredPath).TrimEnd('\') -ieq [IO.Path]::GetFullPath($install).TrimEnd('\')}catch{$pathMatches=$false}
    }
    $markerMatches=!$duplicate -and $fields['Path'] -ieq $install -and $fields['Release'] -eq $release -and $fields['Channel'] -eq 'DesktopUp.Preview'
    $reserved=@('DesktopUp.exe','Uninstall.exe','PREVIEW-README.txt','app','licenses') | Where-Object {
        $path=Join-Path $install $_;[IO.File]::Exists($path) -or [IO.Directory]::Exists($path)
    }
    $state=if($registered){if($pathMatches -and $markerMatches){'Registered'}else{'Mismatch'}}elseif(@($reserved).Count){'UnregisteredFiles'}else{'Absent'}
    [pscustomobject]@{
        State=$state;User=[Security.Principal.WindowsIdentity]::GetCurrent().Name
        PowerShell=$PSVersionTable.PSVersion.ToString();Process64=[Environment]::Is64BitProcess;RegistryView='Registry64'
        UserSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;RegistryKey=$registryKey
        Registered=$registered;RegisteredDirectory=$registeredPath;ExpectedDirectory=$install;MarkerMatches=$markerMatches
        LauncherExists=[IO.File]::Exists((Join-Path $install 'DesktopUp.exe'))
        AppExists=[IO.File]::Exists((Join-Path $install 'app/DesktopUp.exe'))
        UninstallerExists=[IO.File]::Exists((Join-Path $install 'Uninstall.exe'))
        ReservedNames=@($reserved);Profile=$profile
    }
}
# This action is strictly read-only, including when the application is running.
if($Action -eq 'Status'){Get-ReviewInstallation;return}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw '请先从托盘正常退出 DesktopUp；本脚本不会强制关闭。'}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$reviewTools=Enter-DesktopUpToolchain -Offline
try {
    if(!(Test-Path -LiteralPath $root)){
        if($Action -ne 'Open'){throw '请先使用 Open 创建独立验收数据。'}
        New-Item -ItemType Directory -Path $root | Out-Null
        [IO.File]::WriteAllText((Join-Path $root '.content-return-fixture'),'owned uninstall review')
        & (Join-Path $repo 'out/uninstall-content/review-fixture/DesktopUp.SessionFixture.exe') $profile --uninstall-content-review
        if($LASTEXITCODE){throw 'Fixture failed; preserve evidence.'}
    }
    if(!(Test-Path -LiteralPath (Join-Path $root '.content-return-fixture')) -or !(Test-Path -LiteralPath (Join-Path $profile 'state/desktopup.sqlite3'))){throw '验收标记/配置不完整；未修改已有数据。'}
    if($Action -eq 'Folder'){Start-Process explorer.exe -ArgumentList ('"'+$root+'"');return}
    $state=Get-ReviewInstallation
    if($state.State -in @('Mismatch','UnregisteredFiles')){
        $state | Format-List | Out-Host
        throw '安装登记与目录不一致，已停止；没有删除残留、重装覆盖或修改登记。请提供上面的状态信息。'
    }
    if($state.State -eq 'Absent' -and $Action -eq 'Open'){
        Write-Host "将在当前用户环境安装至：$install；复用已有验收数据，不覆盖旧 installed 目录。"
        $p=Start-DesktopUpFixtureProcess -ExecutablePath $setup -DataRoot $profile -Arguments ("/S /DESKTOPSHORTCUT=0 /D=$install")
        if(!$p.WaitForExit(60000)){throw "安装未结束，PID $($p.Id)，请核对。"}
        $p.Refresh();$state=Get-ReviewInstallation
        if($p.ExitCode -ne 0 -or $state.State -ne 'Registered'){
            $state | Format-List | Out-Host
            throw "安装未完成；退出码=$($p.ExitCode)，状态=$($state.State)。未启动或清理文件。"
        }
    }elseif($state.State -eq 'Absent'){throw '未找到本版本安装登记或程序文件；需要安装时使用 Open。'}
    if($Action -eq 'Uninstall'){
        if(!$state.UninstallerExists){throw '本版本仍有安装登记，但卸载器缺失；未重装覆盖或修改登记。'}
        $p=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'Uninstall.exe') -DataRoot $profile -WindowStyle Normal
    }else{
        if(!$state.LauncherExists -or !$state.AppExists){throw '本版本登记存在但程序不完整；未启动或重装覆盖，请先核对 Status。'}
        $p=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $install 'DesktopUp.exe') -DataRoot $profile -WindowStyle Normal
    }
    Write-Host "已打开 $Action，PID $($p.Id)。本次配置：$profile"
    Write-Host '仅通过本脚本的 Open / Uninstall 使用隔离配置；不要直接点击开始菜单、exe 或 Windows 设置的卸载入口（它们使用日常 Preview 配置）。'
} finally {Exit-DesktopUpToolchain $reviewTools}
