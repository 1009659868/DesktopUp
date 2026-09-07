param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    dotnet restore (Join-Path $root 'DesktopUp.sln') -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
    dotnet build (Join-Path $root 'DesktopUp.sln') -c $Configuration -p:Platform=x64 --no-restore
} else {
    # Preserve the separator when dotnet passes -o as the global OutputPath property.
    $outputPath = [IO.Path]::GetFullPath($OutputDirectory, $root).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    dotnet build (Join-Path $root 'src\DesktopUp.App\DesktopUp.App.csproj') -c $Configuration -p:Platform=x64 -o $outputPath
}
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
