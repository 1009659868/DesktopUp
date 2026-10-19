#requires -Version 5.1
# One read-only binding for current M07 animation, manual, safety and resource gates.
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskBuild=Join-Path $taskRepo 'out\verify\v3-04-m07-animation-stable'
$taskManifest=Join-Path $PSScriptRoot 'm07-animation-build.json'
$taskExpected=Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
$taskCount=0
foreach ($taskArea in $taskExpected.PSObject.Properties) {
 foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
  $taskFile=Join-Path (Join-Path $taskBuild $taskArea.Name) $taskEntry.Name
  if (!(Test-Path -LiteralPath $taskFile -PathType Leaf) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
   throw "Stable M07 artifact fingerprint mismatch: $taskFile. Do not fall back to an older package."
  }
  $taskCount++
 }
}
if ($taskCount -ne 24) { throw 'Expected the complete 24-artifact stable manifest.' }
[pscustomobject]@{ Root=$taskBuild; App=(Join-Path $taskBuild 'app\DesktopUp.exe');
 Fixture=(Join-Path $taskBuild 'fixture\DesktopUp.SessionFixture.exe'); VerifiedBinaries=$taskCount;
 Manifest=$taskManifest; ManifestHash=(Get-FileHash -LiteralPath $taskManifest).Hash;
 AppHash=$taskExpected.app.'DesktopUp.dll'; AnimationConfig=(Join-Path $taskRepo 'config\poster-animation.json') }
