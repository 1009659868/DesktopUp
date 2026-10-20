#requires -Version 5.1
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
$taskRoot=Join-Path $taskRepo 'out\verify\v3-04-m08-07-r1'
$taskManifest=Join-Path $PSScriptRoot 'm08-build.json'
$taskExpected=Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
$taskCount=0
foreach ($taskArea in $taskExpected.PSObject.Properties) {
 foreach ($taskEntry in $taskArea.Value.PSObject.Properties) {
  $taskFile=Join-Path (Join-Path $taskRoot $taskArea.Name) $taskEntry.Name
  if (!(Test-Path -LiteralPath $taskFile -PathType Leaf) -or (Get-FileHash -LiteralPath $taskFile).Hash -ne $taskEntry.Value) {
   throw "M08 artifact changed/missing: $taskFile. Do not fall back to an older package."
  }
  $taskCount++
 }
}
if ($taskCount -ne 25) { throw 'Expected 24 binary/resource bindings plus frozen animation configuration.' }
[pscustomobject]@{ Root=$taskRoot; App=(Join-Path $taskRoot 'app\DesktopUp.exe');
 Fixture=(Join-Path $taskRoot 'fixture\DesktopUp.SessionFixture.exe'); VerifiedArtifacts=$taskCount;
 AppHash=$taskExpected.app.'DesktopUp.dll'; ManifestHash=(Get-FileHash -LiteralPath $taskManifest).Hash;
 AnimationConfig=(Join-Path $taskRoot 'app\config\poster-animation.json') }
