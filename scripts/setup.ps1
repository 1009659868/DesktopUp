#requires -Version 5.1
param([switch]$ForPackaging, [switch]$Offline, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'toolchain.ps1')
if ($CheckOnly) {
    $info = Get-DesktopUpToolchain
    $info | Select-Object DotNet, SdkPresent, MakeNsis, NsisPresent, Packages
    return
}
$setup = Enter-DesktopUpToolchain -ForPackaging:$ForPackaging -Offline:$Offline
Push-Location $setup.Repo
try {
    $restoreOptions = @('-p:Platform=x64')
    if ($Offline) { $restoreOptions += @('--configfile', $setup.OfflineConfig, '-p:NuGetAudit=false') }
    & $setup.DotNet restore DesktopUp.sln @restoreOptions
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    if ($ForPackaging) {
        $settings = Get-Content packaging/preview/package-settings.json -Raw | ConvertFrom-Json
        & $setup.DotNet restore src/DesktopUp.App/DesktopUp.App.csproj @restoreOptions -p:PublishProfile=Preview-x64 -p:SelfContained=true "-p:DesktopUpPreviewRuntimeVersion=$($settings.runtimeVersion)"
        if ($LASTEXITCODE -ne 0) { throw 'Packaging dependency restore failed.' }
    }
} finally { Pop-Location; Exit-DesktopUpToolchain $setup }
