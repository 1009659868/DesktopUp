#requires -Version 7.0
param([Parameter(Mandatory)][string]$SourcePackage,
    [Parameter(Mandatory)][ValidatePattern('^2099\d{4}\.\d+$')][string]$Release,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Name,
    [switch]$Legacy)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')
$base=Join-Path $repo 'out/single-install/review'
$output=Join-Path $base $Name
$profile=Join-Path $base 'profile'
if(Test-Path -LiteralPath $output){throw 'Choose a fresh fixture directory; never overwrite evidence.'}
if($output -match '["$\r\n]'){throw 'Unsafe fixture path'}
$source=[IO.Path]::GetFullPath($SourcePackage)
$utf8=[Text.UTF8Encoding]::new($false)
$utf8Bom=[Text.UTF8Encoding]::new($true)
function Write-Generated([string]$path,[string]$text){[IO.File]::WriteAllText($path,$text,$utf8)}
. (Join-Path $repo 'scripts/toolchain.ps1')
$fixtureTools=Enter-DesktopUpToolchain -ForPackaging -Offline
try {
    New-Item -ItemType Directory -Path $output | Out-Null
    $payload=Join-Path $output 'payload'
    Copy-Item -LiteralPath (Join-Path $source 'payload') -Destination $payload -Recurse
    $scope='DesktopUp.SingleInstallReview'
    $product='DesktopUp SingleInstall Review'
    $state='InstallerSingleReview'
    $inputHashes=@(Get-ChildItem (Join-Path $repo 'src/DesktopUp.Setup') -Filter '*.cs' | ForEach-Object { [ordered]@{Name=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
    if(!$Legacy){
        $helper=Join-Path $output 'helper-source'
        New-Item -ItemType Directory -Path $helper|Out-Null
        foreach($file in Get-ChildItem (Join-Path $repo 'src/DesktopUp.Setup') -Filter '*.cs'){
            $text=[IO.File]::ReadAllText($file.FullName)
            if($file.Name -eq 'InstallationMetadata.cs'){
                $text=$text.Replace('StableId = "DesktopUp"','StableId = "'+$scope+'"').Replace('LegacyPrefix = "DesktopUp.Preview."','LegacyPrefix = "'+$scope+'.Preview."')
                $text=$text.Replace('DesktopUp Preview',$product).Replace('"Installer"','"'+$state+'"')
            }
            if($file.Name -eq 'InstallationIdentity.cs'){$text=$text.Replace('"DesktopUp"','"'+$scope+'"')}
            Write-Generated (Join-Path $helper $file.Name) $text
        }
        Write-Generated (Join-Path $helper 'Fixture.csproj') '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>DesktopUp.Setup</AssemblyName><RuntimeIdentifier>win-x64</RuntimeIdentifier><SelfContained>true</SelfContained></PropertyGroup></Project>'
        & $fixtureTools.DotNet publish (Join-Path $helper 'Fixture.csproj') -c Release -p:Platform=x64 -p:DebugType=none -p:DebugSymbols=false -p:NuGetAudit=false "-p:RestoreConfigFile=$($fixtureTools.OfflineConfig)" "-p:PublishDir=$output/helper-publish/" *> (Join-Path $output 'helper-build.log')
        if($LASTEXITCODE -ne 0){throw 'Fixture helper build failed'}
        foreach($file in @('DesktopUp.Setup.exe','DesktopUp.Setup.dll','DesktopUp.Setup.deps.json','DesktopUp.Setup.runtimeconfig.json')){
            Copy-Item -LiteralPath (Join-Path $output ('helper-publish/'+$file)) -Destination (Join-Path $payload 'app')
        }
    }
    # Only identity/shell-entry labels are mechanically substituted. Transaction code is unchanged.
    foreach($fixtureFileName in @('installer.nsi','upgrade.nsh','uninstall-evidence.nsh','launcher.nsi')){
        $text=if($Legacy){(& git -C $repo show ('e0f3a1b:packaging/preview/'+$fixtureFileName)) -join "`n"}else{[IO.File]::ReadAllText((Join-Path $repo ('packaging/preview/'+$fixtureFileName)))}
        $text=$text.Replace('Uninstall\DesktopUp.Preview.','Uninstall\'+$scope+'.Preview.').Replace('Uninstall\DesktopUp"','Uninstall\'+$scope+'"')
        $text=$text.Replace('"DesktopUp.Preview."','"'+$scope+'.Preview."').Replace('"DesktopUp"','"'+$scope+'"')
        $text=$text.Replace('$0 18','$0 '+($scope+'.Preview.').Length).Replace('$0 "" 18','$0 "" '+($scope+'.Preview.').Length)
        $text=$text.Replace('\DesktopUp\Installer','\DesktopUp\'+$state).Replace('DesktopUp Preview',$product)
        $text=$text.Replace('$LOCALAPPDATA\Programs\DesktopUp"','$LOCALAPPDATA\Programs\'+$product+'"')
        $environment='  System::Call ''kernel32::SetEnvironmentVariableW(w "DESKTOPUP_V3_TEST_DATA_ROOT", w "'+$profile+'")'''
        $text=$text.Replace('  SetRegView 64',"  SetRegView 64`n"+$environment)
        if($fixtureFileName -eq 'launcher.nsi'){$text=$text.Replace("Section`n","Section`n"+$environment+"`n")}
        # The original channel marker remains Preview, so production identity validation is exercised.
        Write-Generated (Join-Path $output $fixtureFileName) $text
    }
    & $fixtureTools.MakeNsis /V2 /WX /INPUTCHARSET UTF8 "/DRELEASE_ID=$Release" /DFILE_VERSION=0.1.0.2 "/DLAUNCHER_OUTPUT=$payload/DesktopUp.exe" (Join-Path $output 'launcher.nsi') *> (Join-Path $output 'launcher.log')
    if($LASTEXITCODE -ne 0){throw 'Fixture launcher build failed'}
    $build=Get-Content (Join-Path $payload 'app/preview-build.json') -Raw|ConvertFrom-Json
    $build.Release=$Release
    Write-Generated (Join-Path $payload 'app/preview-build.json') ($build|ConvertTo-Json -Depth 6)
    $files=@(Get-ChildItem -LiteralPath $payload -File -Recurse | Where-Object Name -ne 'payload-manifest.json')
    $manifest=@($files|ForEach-Object {[ordered]@{Path=$_.FullName.Substring($payload.Length+1).Replace('\','/');Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    Write-Generated (Join-Path $payload 'app/payload-manifest.json') (ConvertTo-Json -InputObject $manifest -Depth 5)
    foreach($fixtureFileName in @('install-files.nsh','uninstall-files.nsh','uninstall-directory-guards.nsh')){
        $text=[IO.File]::ReadAllText((Join-Path $source ('evidence/'+$fixtureFileName))).Replace((Join-Path $source 'payload'),$payload)
        [IO.File]::WriteAllText((Join-Path $output $fixtureFileName),$text,$utf8Bom)
    }
    $setup=Join-Path $output 'Review-Only-Setup.exe'
    & $fixtureTools.MakeNsis /V2 /WX /INPUTCHARSET UTF8 "/DRELEASE_ID=$Release" /DFILE_VERSION=0.1.0.2 "/DSETUP_OUTPUT=$setup" "/DINSTALL_FILES=$output\install-files.nsh" "/DUNINSTALL_FILES=$output\uninstall-files.nsh" "/DUNINSTALL_GUARDS=$output\uninstall-directory-guards.nsh" /DMAX_INSTALL_PATH=124 /DPAYLOAD_KB=350000 (Join-Path $output 'installer.nsi') *> (Join-Path $output 'installer.log')
    if($LASTEXITCODE -ne 0){throw 'Fixture installer build failed'}
    [ordered]@{Scope=$scope;Profile=$profile;Release=$Release;Legacy=[bool]$Legacy;SourcePackage=$source;SourceHashes=$inputHashes;Installer=$setup;SHA256=(Get-FileHash -LiteralPath $setup).Hash;Notice='Isolated identity fixture, not the distributable installer; transaction sources unchanged.'} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'fixture.json') -Encoding utf8
    if(!$Legacy){Copy-Item -LiteralPath (Join-Path $output 'fixture.json') -Destination (Join-Path $base 'active-fixture.json') -Force}
    Get-Content (Join-Path $output 'fixture.json')
} finally {Exit-DesktopUpToolchain $fixtureTools}
