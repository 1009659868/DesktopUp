#requires -Version 5.1
param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskApp=Join-Path $taskRepo 'out\verify\v3-04-m07\app'
$taskPins=@((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'm07-build.json') -Raw | ConvertFrom-Json).PSObject.Properties)
foreach ($taskEntry in $taskPins) {
 if ((Get-FileHash -LiteralPath (Join-Path $taskApp $taskEntry.Name)).Hash -ne $taskEntry.Value) { throw 'M07 build changed; re-freeze before testing.' }
}
$taskFixture=Join-Path $taskRepo 'tests\DesktopUp.SessionFixture\bin\x64\Release\net10.0-windows10.0.22621.0\DesktopUp.SessionFixture.exe'
if (-not (Test-Path -LiteralPath $taskFixture)) { throw 'Build SessionFixture first.' }
if ($CheckOnly) { [pscustomobject]@{ Result='PREFLIGHT PASS'; VerifiedBinaries=$taskPins.Count; Launched=$false }; return }
$taskOutput=Join-Path $taskRepo ('out\verify\v3-04-m07\current-env-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
& (Join-Path $PSScriptRoot 'measure-m07.ps1') -ExecutablePath (Join-Path $taskApp 'DesktopUp.exe') -FixtureExecutablePath $taskFixture -OutputRoot $taskOutput -Repeats 2
