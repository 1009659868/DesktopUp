#requires -Version 5.1
param(
    [ValidatePattern('^\d{8}\.\d+$')][string]$ReleaseId,
    [string]$OutputRoot,
    [string]$SettingsPath,
    [ValidateSet('lamp','basic-box','night-ops')][string]$DefaultTemplate,
    [Nullable[double]]$BackgroundOpacityPercent,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$RuntimeVersion,
    [string]$MakeNsis,
    [switch]$Offline,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$pkgRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
if (!$SettingsPath) { $SettingsPath = Join-Path $pkgRepo 'packaging/preview/package-settings.json' }
$pkgSettingsText = Get-Content -LiteralPath $SettingsPath -Raw -Encoding UTF8
$pkgSettings = $pkgSettingsText | ConvertFrom-Json
$pkgKnown = @('schemaVersion','version','fileVersion','runtimeVersion','defaultTemplate','backgroundOpacityPercent')
$pkgNames = @($pkgSettings.PSObject.Properties.Name)
if ($pkgSettings.schemaVersion -ne 1 -or $pkgNames.Count -ne $pkgKnown.Count -or
    @($pkgNames | Where-Object { $_ -cnotin $pkgKnown }).Count -or
    [regex]::Matches($pkgSettingsText,'"[^"\r\n]+"\s*:').Count -ne $pkgKnown.Count) { throw 'Invalid/duplicate/unknown package setting.' }
if ($pkgSettings.version -cnotmatch '^\d+\.\d+\.\d+$' -or $pkgSettings.fileVersion -cnotmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Use numeric version and four-part fileVersion.' }
foreach ($pkgPart in $pkgSettings.fileVersion.Split('.')) { if ([long]$pkgPart -gt 65535) { throw 'fileVersion component exceeds 65535.' } }
if (!$PSBoundParameters.ContainsKey('DefaultTemplate')) { $DefaultTemplate = $pkgSettings.defaultTemplate }
if (!$PSBoundParameters.ContainsKey('BackgroundOpacityPercent')) { $BackgroundOpacityPercent = $pkgSettings.backgroundOpacityPercent }
if (!$RuntimeVersion) { $RuntimeVersion = $pkgSettings.runtimeVersion }
if ($DefaultTemplate -cnotin @('lamp','basic-box','night-ops') -or $RuntimeVersion -cnotmatch '^\d+\.\d+\.\d+$') { throw 'Unsupported template/runtime version.' }
if ($DefaultTemplate -ceq 'lamp') {
    if ($null -eq $BackgroundOpacityPercent -or [double]::IsNaN($BackgroundOpacityPercent) -or [double]::IsInfinity($BackgroundOpacityPercent) -or $BackgroundOpacityPercent -lt 0 -or $BackgroundOpacityPercent -gt 100) { throw 'Lamp background opacity must be 0..100 (not transparency).' }
} elseif ($null -ne $BackgroundOpacityPercent) { throw 'Other built-in templates do not support opacity; set backgroundOpacityPercent to null.' }
if (!$ReleaseId) { $pkgNow=Get-Date; $ReleaseId=$pkgNow.ToString('yyyyMMdd')+'.'+[int]$pkgNow.TimeOfDay.TotalSeconds }
if (!$OutputRoot) { $OutputRoot = Join-Path $pkgRepo ('out/releases/preview-'+$ReleaseId) }
elseif (![IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot = Join-Path $pkgRepo $OutputRoot }
$pkgOutput = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$pkgAllowed = Join-Path $pkgRepo 'out'
if (!$pkgOutput.StartsWith($pkgAllowed+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputRoot must be a new directory below repository/out.' }
if (Test-Path -LiteralPath $pkgOutput) { throw 'Preserve old builds: choose a new OutputRoot.' }
. (Join-Path $PSScriptRoot 'toolchain.ps1')
$pkgToolInfo = Get-DesktopUpToolchain -MakeNsis $MakeNsis
$pkgExplicitNsis = $MakeNsis
$MakeNsis = $pkgToolInfo.MakeNsis
foreach ($pkgPath in @($pkgRepo, $pkgOutput, $MakeNsis)) {
    if ($pkgPath -match '["$\r\n]') { throw 'Unsafe installer source path.' }
}
$pkgResolved = [ordered]@{ ReleaseId=$ReleaseId; Version=$pkgSettings.version; FileVersion=$pkgSettings.fileVersion;
    RuntimeVersion=$RuntimeVersion; DefaultTemplate=$DefaultTemplate; BackgroundOpacityPercent=$BackgroundOpacityPercent;
    OutputRoot=$pkgOutput; MakeNsis=$MakeNsis; DotNet=$pkgToolInfo.DotNet; SdkVersion=$pkgToolInfo.Manifest.sdk.version;
    SdkPresent=$pkgToolInfo.SdkPresent; NsisPresent=$pkgToolInfo.NsisPresent;
    SettingsSHA256=(Get-FileHash -LiteralPath $SettingsPath).Hash }
if ($CheckOnly) { [pscustomobject]$pkgResolved; return }
# Snapshot hashes before publish; fail if sources change while this package is built.
function Get-SourceBinding {
    $pkgRoots = @('src','Modules','config','packaging','scripts')
    @(foreach ($pkgArea in $pkgRoots) {
        Get-ChildItem -LiteralPath (Join-Path $pkgRepo $pkgArea) -Recurse -File |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
            ForEach-Object { [ordered]@{ Path=$_.FullName.Substring($pkgRepo.Length+1).Replace('\','/'); SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash } }
    }; foreach ($pkgName in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','DesktopUp.sln','global.json','NuGet.Config','Package-Preview.cmd','LICENSE','THIRD_PARTY_NOTICES.md')) {
        [ordered]@{ Path=$pkgName; SHA256=(Get-FileHash -LiteralPath (Join-Path $pkgRepo $pkgName)).Hash }
    }) | Sort-Object { $_.Path }
}
$pkgSource = @(Get-SourceBinding)
$pkgTools = Enter-DesktopUpToolchain -ForPackaging -MakeNsis $pkgExplicitNsis -Offline:$Offline
try {
$pkgResolved.SdkPresent = $true
$pkgResolved.NsisPresent = $true
$pkgBuildOptions = @()
if ($Offline) { $pkgBuildOptions += @("-p:RestoreConfigFile=$($pkgTools.OfflineConfig)", '-p:NuGetAudit=false') }
$pkgPayload = Join-Path $pkgOutput 'payload'
$pkgApp = Join-Path $pkgPayload 'app'
$pkgBuildPath = $pkgOutput.Replace('\','/') + '/build/'
$pkgPublishPath = $pkgApp.Replace('\','/') + '/'
$pkgEvidence = Join-Path $pkgOutput 'evidence'
New-Item -ItemType Directory -Path $pkgEvidence -Force | Out-Null
$pkgSource | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $pkgEvidence 'source-manifest.json') -Encoding utf8
$pkgResolved | ConvertTo-Json | Set-Content (Join-Path $pkgEvidence 'resolved-settings.json') -Encoding utf8
Push-Location $pkgRepo
try {
    $pkgHead = (& git rev-parse HEAD).Trim()
    $pkgStatus = @(& git status --porcelain)
    # WinUI's incremental PRI cache can survive a framework-dependent build. Rebuild
    # with the exact self-contained publish properties, then publish those same outputs.
    & $pkgTools.DotNet build src/DesktopUp.App/DesktopUp.App.csproj @pkgBuildOptions --no-incremental -c Release -p:Platform=x64 -p:PublishProfile=Preview-x64 -p:DesktopUpPreview=true -p:SelfContained=true -p:DebugType=none -p:DebugSymbols=false "-p:DesktopUpPreviewRuntimeVersion=$RuntimeVersion" "-p:Version=$($pkgSettings.version)-preview.$ReleaseId" "-p:FileVersion=$($pkgSettings.fileVersion)" "-p:OutputPath=$pkgBuildPath" "-p:PublishDir=$pkgPublishPath" 2>&1 |
        Tee-Object -FilePath (Join-Path $pkgEvidence 'rebuild.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained rebuild failed; partial evidence preserved.' }
    & $pkgTools.DotNet publish src/DesktopUp.App/DesktopUp.App.csproj @pkgBuildOptions --no-build -c Release -p:Platform=x64 -p:PublishProfile=Preview-x64 -p:DesktopUpPreview=true -p:SelfContained=true -p:DebugType=none -p:DebugSymbols=false "-p:DesktopUpPreviewRuntimeVersion=$RuntimeVersion" "-p:Version=$($pkgSettings.version)-preview.$ReleaseId" "-p:FileVersion=$($pkgSettings.fileVersion)" "-p:OutputPath=$pkgBuildPath" "-p:PublishDir=$pkgPublishPath" 2>&1 |
        Tee-Object -FilePath (Join-Path $pkgEvidence 'publish.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed; partial evidence preserved.' }
    $pkgRuntime = Get-Content -LiteralPath (Join-Path $pkgApp 'DesktopUp.runtimeconfig.json') -Raw | ConvertFrom-Json
    $pkgFramework = @($pkgRuntime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App')
    if ($pkgFramework.Count -ne 1 -or $pkgFramework[0].version -cne $RuntimeVersion) { throw 'Unexpected self-contained .NET version.' }
    foreach ($pkgRequired in @('DesktopUp.exe','DesktopUp.dll','coreclr.dll','hostfxr.dll','Microsoft.ui.xaml.dll','DesktopUp.pri')) {
        if (!(Test-Path -LiteralPath (Join-Path $pkgApp $pkgRequired))) { throw "Missing runtime/presentation artifact: $pkgRequired" }
    }
    $pkgLauncher = Join-Path $pkgPayload 'DesktopUp.exe'
    & $MakeNsis /V3 /WX /INPUTCHARSET UTF8 /OUTPUTCHARSET UTF8 "/DRELEASE_ID=$ReleaseId" "/DFILE_VERSION=$($pkgSettings.fileVersion)" "/DLAUNCHER_OUTPUT=$pkgLauncher" (Join-Path $pkgRepo 'packaging/preview/launcher.nsi') 2>&1 |
        Tee-Object -FilePath (Join-Path $pkgEvidence 'launcher.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Root launcher creation failed; evidence preserved.' }
    # The short-lived root launcher delegates to app/DesktopUp.exe; WinUI needs its native assets alongside it.
    if (Get-ChildItem -LiteralPath $pkgPayload -Recurse -File | Where-Object { $_.Extension -in @('.pdb','.sqlite','.sqlite3','.db','.jsonl') -or $_.Name -like '*SessionFixture*' }) { throw 'Publish unexpectedly contains symbols, user/test data or fixtures.' }
    if (Get-ChildItem -LiteralPath $pkgPayload -Recurse -Attributes ReparsePoint) { throw 'Payload must not contain reparse points.' }
    Copy-Item -LiteralPath (Join-Path $pkgRepo 'packaging/preview/PREVIEW-README.txt') -Destination $pkgPayload
    [ordered]@{ schemaVersion=1; templateKey=$DefaultTemplate; backgroundOpacityPercent=$BackgroundOpacityPercent } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pkgApp 'config/preview-defaults.json') -Encoding utf8
    # Include package-provided licenses/notices, original nuspec attribution, and SPDX metadata.
    $pkgLicenses = Join-Path $pkgPayload 'licenses'
    New-Item -ItemType Directory -Path $pkgLicenses | Out-Null
    $pkgAssets = Get-Content -LiteralPath (Join-Path $pkgRepo 'src/DesktopUp.App/obj/project.assets.json') -Raw | ConvertFrom-Json
    $pkgCache = @($pkgAssets.packageFolders.PSObject.Properties.Name)[0]
    $pkgDependencies = @()
    $pkgPackages = @($pkgAssets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { $_.Value.path }) + @('microsoft.netcore.app.runtime.win-x64/'+$RuntimeVersion)
    foreach ($pkgPackage in ($pkgPackages | Sort-Object -Unique)) {
        $pkgFolder = Join-Path $pkgCache $pkgPackage
        $pkgLicenseFolder = Join-Path $pkgLicenses ($pkgPackage.Replace('/','-'))
        New-Item -ItemType Directory -Path $pkgLicenseFolder | Out-Null
        $pkgNuspec = Get-ChildItem -LiteralPath $pkgFolder -Filter '*.nuspec' -File | Select-Object -First 1
        if (!$pkgNuspec) { throw "Missing package metadata: $pkgPackage" }
        [xml]$pkgSpec = Get-Content -LiteralPath $pkgNuspec.FullName -Raw
        Copy-Item -LiteralPath $pkgNuspec.FullName -Destination $pkgLicenseFolder
        Get-ChildItem -LiteralPath $pkgFolder -File | Where-Object Name -Match '(?i)license|notice|copying' | Copy-Item -Destination $pkgLicenseFolder
        $pkgDependencies += [ordered]@{ Package=$pkgPackage; Authors=[string]$pkgSpec.package.metadata.authors; License=[string]$pkgSpec.package.metadata.license.InnerText; ProjectUrl=[string]$pkgSpec.package.metadata.projectUrl }
    }
    Copy-Item -LiteralPath (Join-Path (Split-Path $MakeNsis) 'COPYING') -Destination (Join-Path $pkgLicenses 'NSIS-COPYING.txt')
    Copy-Item -Path (Join-Path $pkgRepo 'packaging/preview/licenses/*') -Destination $pkgLicenses
    Copy-Item -LiteralPath (Join-Path $pkgRepo 'LICENSE') -Destination (Join-Path $pkgLicenses 'DesktopUp-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $pkgRepo 'THIRD_PARTY_NOTICES.md') -Destination $pkgLicenses
    $pkgDependencies | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $pkgLicenses 'packages.json') -Encoding utf8
    $pkgWinAppSdk = @($pkgPackages | Where-Object { $_ -match '^microsoft.windowsappsdk/' })[0].Split('/')[1]
    [ordered]@{ Release=$ReleaseId; Channel='Preview'; Architecture='AMD64'; MinimumOS='Windows 11'; DotNet=$RuntimeVersion; WindowsAppSDK=$pkgWinAppSdk; SourceCommit=$pkgHead; IncludesUncommittedChanges=($pkgStatus.Count -gt 0); DataRoot='%LOCALAPPDATA%\DesktopUp\Preview'; DefaultStorageBase='%LOCALAPPDATA%\DesktopUp\Storage\preview'; ApplicationId='DesktopUp.V3.Preview'; DefaultTemplate=$DefaultTemplate; BackgroundOpacityPercent=$BackgroundOpacityPercent; CustomInstallDirectory=$true; Content='Real content; empty initial profile; no demo cards'; Signed=$false } |
        ConvertTo-Json | Set-Content (Join-Path $pkgApp 'preview-build.json') -Encoding utf8
    $pkgFiles = @(Get-ChildItem -LiteralPath $pkgPayload -Recurse -File | Sort-Object FullName)
    $pkgManifest = @(foreach ($pkgFile in $pkgFiles) {
        [ordered]@{ Path=$pkgFile.FullName.Substring($pkgPayload.Length+1).Replace('\','/'); Length=$pkgFile.Length; SHA256=(Get-FileHash -LiteralPath $pkgFile.FullName).Hash }
    })
    $pkgManifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $pkgApp 'payload-manifest.json') -Encoding utf8
    # Keep this namespace aligned with installer.nsi's nonempty-directory collision checks.
    if (Get-ChildItem -LiteralPath $pkgPayload -Force | Where-Object Name -NotIn @('DesktopUp.exe','PREVIEW-README.txt','app','licenses')) {
        throw 'Unexpected payload root entry; update owned-name guards before packaging.'
    }
    $pkgFiles = @(Get-ChildItem -LiteralPath $pkgPayload -Recurse -File | Sort-Object FullName)
    $pkgInstall = [Collections.Generic.List[string]]::new()
    $pkgUninstall = [Collections.Generic.List[string]]::new()
    $pkgGuards = [Collections.Generic.List[string]]::new()
    foreach ($pkgFile in $pkgFiles) {
        $pkgRelative = $pkgFile.FullName.Substring($pkgPayload.Length+1)
        if ($pkgRelative -match '["$\r\n]' -or $pkgRelative.Split('\') -contains '..') { throw 'Unsafe payload filename.' }
        $pkgSubdir = Split-Path -Parent $pkgRelative
        $pkgInstall.Add('SetOutPath "$INSTDIR\'+$pkgSubdir+'"')
        $pkgInstall.Add('File "'+$pkgFile.FullName+'"')
        $pkgInstall.Add('IfErrors installation_failed')
        $pkgUninstall.Add('Delete "$INSTDIR\'+$pkgRelative+'"')
        $pkgUninstall.Add('IfErrors uninstall_failed')
    }
    Get-ChildItem -LiteralPath $pkgPayload -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
        $pkgGuards.Add('StrCpy $GuardPath "$INSTDIR\'+$_.FullName.Substring($pkgPayload.Length+1)+'"')
        $pkgGuards.Add('Call un.RequireSafeDirectory')
        $pkgUninstall.Add('ClearErrors')
        $pkgUninstall.Add('RMDir "$INSTDIR\'+$_.FullName.Substring($pkgPayload.Length+1)+'"')
    }
    $pkgInstallList = Join-Path $pkgEvidence 'install-files.nsh'
    $pkgUninstallList = Join-Path $pkgEvidence 'uninstall-files.nsh'
    $pkgGuardList = Join-Path $pkgEvidence 'uninstall-directory-guards.nsh'
    $pkgUtf8Bom = New-Object System.Text.UTF8Encoding($true)
    [IO.File]::WriteAllLines($pkgInstallList, $pkgInstall, $pkgUtf8Bom)
    [IO.File]::WriteAllLines($pkgUninstallList, $pkgUninstall, $pkgUtf8Bom)
    [IO.File]::WriteAllLines($pkgGuardList, $pkgGuards, $pkgUtf8Bom)
    $pkgSetup = Join-Path $pkgOutput "DesktopUp-Preview-$ReleaseId-win-x64-Setup.exe"
    $pkgSizeKb = [int][Math]::Ceiling(($pkgFiles | Measure-Object Length -Sum).Sum / 1024)
    $pkgMaximumRelative = ($pkgFiles | ForEach-Object { $_.FullName.Length-$pkgPayload.Length } | Measure-Object -Maximum).Maximum
    $pkgMaximumInstall = [int][Math]::Min(180, 240-$pkgMaximumRelative)
    & $MakeNsis /V3 /WX /INPUTCHARSET UTF8 /OUTPUTCHARSET UTF8 "/DRELEASE_ID=$ReleaseId" "/DFILE_VERSION=$($pkgSettings.fileVersion)" "/DSETUP_OUTPUT=$pkgSetup" "/DINSTALL_FILES=$pkgInstallList" "/DUNINSTALL_FILES=$pkgUninstallList" "/DUNINSTALL_GUARDS=$pkgGuardList" "/DMAX_INSTALL_PATH=$pkgMaximumInstall" "/DPAYLOAD_KB=$pkgSizeKb" (Join-Path $pkgRepo 'packaging/preview/installer.nsi') 2>&1 |
        Tee-Object -FilePath (Join-Path $pkgEvidence 'nsis.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Installer compile failed; evidence preserved.' }
    if (($pkgSource | ConvertTo-Json -Compress) -cne (@(Get-SourceBinding) | ConvertTo-Json -Compress)) { throw 'Sources changed during packaging; do not distribute this build.' }
    $pkgHash = (Get-FileHash -LiteralPath $pkgSetup).Hash
    "$pkgHash  $([IO.Path]::GetFileName($pkgSetup))" | Set-Content -LiteralPath (Join-Path $pkgOutput 'SHA256SUMS.txt') -Encoding ascii
    [ordered]@{ Installer=$pkgSetup; Bytes=(Get-Item -LiteralPath $pkgSetup).Length; SHA256=$pkgHash; Files=$pkgFiles.Count; AppSHA256=(Get-FileHash (Join-Path $pkgApp 'DesktopUp.dll')).Hash; Verified=$false } |
        ConvertTo-Json | Set-Content (Join-Path $pkgEvidence 'package.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $pkgEvidence 'package.json')
} finally { Pop-Location }
} finally { Exit-DesktopUpToolchain $pkgTools }
