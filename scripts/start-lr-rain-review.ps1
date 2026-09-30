#requires -Version 7
param([Parameter(Mandatory)][string]$EvidenceRoot, [Parameter(Mandatory)][string]$DataRoot, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$reviewRoot = (Resolve-Path -LiteralPath $EvidenceRoot).Path.TrimEnd('\')
function Resolve-RainProof([string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)) { throw 'Expected a relative sealed path.' }
    $reviewPath = [IO.Path]::GetFullPath((Join-Path $reviewRoot $relative))
    if (!$reviewPath.StartsWith($reviewRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence path escaped root.' }
    return $reviewPath
}
$reviewSealPath = Join-Path $reviewRoot 'rain-review.json'
$reviewSeal = Get-Content -LiteralPath $reviewSealPath -Raw | ConvertFrom-Json
if ($reviewSeal.Schema -ne 1) { throw 'Unknown Rain seal.' }
$reviewCandidate = Resolve-RainProof $reviewSeal.Candidate
$reviewManifest = Get-Content -LiteralPath (Resolve-RainProof $reviewSeal.Artifacts) -Raw | ConvertFrom-Json
$reviewActual = @(Get-ChildItem -LiteralPath $reviewCandidate -Recurse -File | ForEach-Object { $_.FullName.Substring($reviewCandidate.Length + 1).Replace('\','/') } | Sort-Object)
if (Compare-Object $reviewActual @($reviewManifest.Path | Sort-Object)) { throw 'Runtime file set changed.' }
foreach ($reviewFile in $reviewManifest) {
    $reviewPath = Resolve-RainProof ($reviewSeal.Candidate + '/' + $reviewFile.Path)
    if ((Get-FileHash -LiteralPath $reviewPath).Hash -ine $reviewFile.SHA256) { throw 'Runtime artifact changed.' }
}
$reviewRequired = @('Contracts','Rain','Resources','Lamp','Recipes','Composition','Browse','Layout','R4','RemoveLamp','RemoveRain')
if (Compare-Object ($reviewRequired | Sort-Object) @($reviewSeal.Proofs.Name | Sort-Object)) { throw 'Rain required gates missing.' }
foreach ($reviewProof in $reviewSeal.Proofs) {
    $reviewPath = Resolve-RainProof $reviewProof.Path
    if ((Get-FileHash -LiteralPath $reviewPath).Hash -ine $reviewProof.SHA256) { throw 'Sealed proof changed.' }
}
$reviewHash = (Get-FileHash -LiteralPath (Join-Path $reviewCandidate 'app/DesktopUp.dll')).Hash
if ($reviewHash -ine $reviewSeal.AppSHA256) { throw 'Wrong selected assembly.' }
$reviewData = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$reviewAllowed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../out')).TrimEnd('\') + '\'
if (!$reviewData.StartsWith($reviewAllowed, [StringComparison]::OrdinalIgnoreCase) -or
    $reviewData.StartsWith($reviewCandidate + '\', [StringComparison]::OrdinalIgnoreCase) -or $reviewData -eq $reviewCandidate -or $reviewData -eq $reviewRoot) { throw 'Use a separate review data directory under out.' }
$reviewBinding = [pscustomobject]@{ AppSHA256 = $reviewHash; SealSHA256 = (Get-FileHash -LiteralPath $reviewSealPath).Hash;
    Scope = 'Rain native/manual PENDING; isolated DEMO only; R7 not certified.' }
if (Test-Path -LiteralPath $reviewData) {
    $previous = Get-Content -LiteralPath (Join-Path $reviewData 'rain-review-binding.json') -Raw | ConvertFrom-Json
    if ($previous.AppSHA256 -ine $reviewHash -or $previous.SealSHA256 -ine $reviewBinding.SealSHA256) { throw 'Existing review belongs to another candidate.' }
}
if ($CheckOnly) { [pscustomobject]@{ Result = 'READY - NOT STARTED'; Binding = $reviewBinding; DataRoot = $reviewData }; return }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit current DesktopUp normally first; no process stopped.' }
if (!(Test-Path -LiteralPath $reviewData)) {
    & (Join-Path $reviewCandidate 'module-fixture/DesktopUp.SceneModuleFixture.exe') seed $reviewData rain
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed; preserve the directory.' }
    $reviewBinding | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reviewData 'rain-review-binding.json')
}
$reviewArgs = '--show-control-center --animation-config="' + (Join-Path $reviewCandidate 'app/config/poster-animation.json') + '"'
$reviewProcess = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $reviewCandidate 'app/DesktopUp.exe') -DataRoot $reviewData -Arguments $reviewArgs -WindowStyle Hidden
[pscustomobject]@{ Result = 'STARTED - RAIN USER VERIFICATION PENDING'; ProcessId = $reviewProcess.Id; DataRoot = $reviewData; Binding = $reviewBinding }
