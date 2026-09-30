#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][string]$DataRoot,
    [ValidateSet('resource-three','three','hundred','empty')][string]$Scenario='resource-three',
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskEvidence=(Resolve-Path -LiteralPath $EvidenceRoot).Path.TrimEnd('\')
function Resolve-R6Evidence([string]$relative) {
    if([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)){throw 'Evidence paths must be relative.'}
    $resolved=[IO.Path]::GetFullPath((Join-Path $taskEvidence $relative))
    if(!$resolved.StartsWith($taskEvidence+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence path escaped its root.'}
    return $resolved
}
$taskSealPath=Join-Path $taskEvidence 'r6-review.json'
$taskSeal=Get-Content -LiteralPath $taskSealPath -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskSeal.Schema -ne 1){throw 'Unknown R6 review seal.'}
if(@($taskSeal.UiGates.PSObject.Properties).Count -ne 8){throw 'All eight UI/interaction gates are required.'}
$taskCandidate=Resolve-R6Evidence $taskSeal.Candidate
# Reuse the previous UI-C gate: full artifact set, real handler/protocol/composition and restart proofs.
$taskUiArgs=@{CandidateDirectory=$taskCandidate;DataRoot=$DataRoot;CheckOnly=$true}
foreach($taskEntry in $taskSeal.UiGates.PSObject.Properties) {
    if($taskEntry.Name -notin @('ProtocolEvidence','FieldsEvidence','RejectedHeatEvidence','UiEvidence',
        'LampEvidence','CompositionEvidence','DailyEvidence','RecipeEvidence')){throw 'Unknown UI gate.'}
    $taskUiArgs[$taskEntry.Name]=Resolve-R6Evidence $taskEntry.Value
}
$null=& (Join-Path $PSScriptRoot 'start-lr-r5-review.ps1') @taskUiArgs
$taskHash=(Get-FileHash -LiteralPath (Join-Path $taskCandidate 'app/DesktopUp.dll') -Algorithm SHA256).Hash
if($taskHash -ine $taskSeal.AppSha256){throw 'R6 review candidate differs.'}
$taskRequiredProofs=@($taskSeal.Browse)+@($taskSeal.Sources)+@($taskSeal.Layout,$taskSeal.Contracts,$taskSeal.R4)
if($taskRequiredProofs.Count -ne 7 -or (Compare-Object ($taskRequiredProofs | Sort-Object) (@($taskSeal.Proofs.Path) | Sort-Object))){
    throw 'R6 evidence is incomplete or not sealed.'
}
foreach($taskProof in $taskSeal.Proofs) {
    $taskPath=Resolve-R6Evidence $taskProof.Path
    if($taskProof.Sha256 -notmatch '^[a-fA-F0-9]{64}$' -or
        (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ine $taskProof.Sha256){throw 'Sealed R6 evidence changed.'}
}
$taskBrowse=Get-Content -LiteralPath (Resolve-R6Evidence $taskSeal.Browse) -Raw | ConvertFrom-Json
if(!$taskBrowse.Passed -or !$taskBrowse.ArtifactsStable -or !$taskBrowse.CasesComplete -or
    !$taskBrowse.CapturesComplete -or $taskBrowse.ExpectedAssemblySha256 -ine $taskHash){throw 'Browsing/R4/space gate incomplete.'}
$taskCounts=@()
foreach($taskPath in $taskSeal.Sources) {
    $taskSource=Get-Content -LiteralPath (Resolve-R6Evidence $taskPath) -Raw | ConvertFrom-Json
    if(!$taskSource.Passed -or !$taskSource.ArtifactsStable -or !$taskSource.CasesComplete -or
        $taskSource.ExpectedAssemblySha256 -ine $taskHash -or $taskSource.Cleanup.activeResources -ne 0){throw 'Source gate incomplete.'}
    $taskCounts+= $taskSource.Observation.observation.posters
}
if((@($taskCounts | Sort-Object) -join ',') -cne '1,3,6'){throw 'All 1/3/6 source baselines are required.'}
$taskLayout=Get-Content -LiteralPath (Resolve-R6Evidence $taskSeal.Layout) -Raw | ConvertFrom-Json
if(@($taskLayout).Count -ne 14 -or @($taskLayout | Where-Object {
    $_.AppHash -ine $taskHash -or $_.Result -ne 'PASS - PRODUCTION COMMAND/CANVAS ONLY' -or
    $_.ExitCode -ne 0 -or $_.ResourcesAfterExit -ne 0 -or $_.ResourceSamples -ne 12 }).Count){throw 'Full layout/resource/restart baseline incomplete.'}
[xml]$taskTests=Get-Content -LiteralPath (Resolve-R6Evidence $taskSeal.Contracts) -Raw
if([int]$taskTests.TestRun.ResultSummary.Counters.failed -ne 0 -or
    [int]$taskTests.TestRun.ResultSummary.Counters.passed -lt 2206){throw 'Contract run is incomplete.'}
$taskR4=Get-Content -LiteralPath (Resolve-R6Evidence $taskSeal.R4) -Raw | ConvertFrom-Json
if($taskR4.Result -ne 'PASS' -or $taskR4.Cases.Count -ne 8 -or $taskR4.ActiveResourcesAfterExit -ne 0){throw 'R4 fault gate incomplete.'}
$taskData=[IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$taskBinding=[pscustomobject]@{AppSha256=$taskHash;Scenario=$Scenario;
    SealSha256=(Get-FileHash -LiteralPath $taskSealPath -Algorithm SHA256).Hash;
    Scope='R6 native/manual continuous verification PENDING; DEMO only; no real files or completed milestone.'}
if($CheckOnly){[pscustomobject]@{Result='READY - NOT STARTED';Binding=$taskBinding};return}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit the existing DesktopUp normally; no process stopped.'}
if(!(Test-Path -LiteralPath $taskData)) {
    & (Join-Path $taskCandidate 'fixture/DesktopUp.SessionFixture.exe') $taskData --content-layout $Scenario | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Review fixture creation failed; preserve partial data.'}
    $taskBinding | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskData 'review-candidate.json') -Encoding UTF8
} else {
    $taskPrevious=Get-Content -LiteralPath (Join-Path $taskData 'review-candidate.json') -Raw | ConvertFrom-Json
    if($taskPrevious.AppSha256 -ine $taskHash -or $taskPrevious.SealSha256 -ine $taskBinding.SealSha256 -or
        $taskPrevious.Scenario -cne $Scenario){throw 'Existing data belongs to another review; preserve it and use a fresh directory.'}
}
$taskArguments='--show-control-center --animation-config="'+(Join-Path $taskCandidate 'app/config/poster-animation.json')+'"'
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskCandidate 'app/DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - R6 USER VERIFICATION PENDING';ProcessId=$taskProcess.Id;DataRoot=$taskData;Binding=$taskBinding}
