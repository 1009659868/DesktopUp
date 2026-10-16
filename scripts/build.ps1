#requires -Version 5.1
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [switch]$Offline,
    [switch]$ReviewTools
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'toolchain.ps1')
$buildTools = Enter-DesktopUpToolchain -Offline:$Offline
Push-Location $root
try {
$buildOptions = @('-p:Platform=x64', '-p:WindowsAppSDKSelfContained=true')
$buildOptions += "-p:DesktopUpReviewTools=$($ReviewTools.IsPresent.ToString().ToLowerInvariant())"
if ($Offline) { $buildOptions += @("-p:RestoreConfigFile=$($buildTools.OfflineConfig)", '-p:NuGetAudit=false') }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    & $buildTools.DotNet restore (Join-Path $root 'DesktopUp.sln') @buildOptions
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
    & $buildTools.DotNet build (Join-Path $root 'DesktopUp.sln') -c $Configuration @buildOptions --no-restore
} else {
    # Preserve the separator when dotnet passes -o as the global OutputPath property.
    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }
    # Forward slash also survives PS 5.1's native quoting for a path containing spaces.
    $outputPath = [IO.Path]::GetFullPath($resolvedOutput).TrimEnd([char[]]'\/').Replace('\','/') + '/'
    & $buildTools.DotNet build (Join-Path $root 'src\DesktopUp.App\DesktopUp.App.csproj') -c $Configuration @buildOptions -o $outputPath
}
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
} finally { Pop-Location; Exit-DesktopUpToolchain $buildTools }
