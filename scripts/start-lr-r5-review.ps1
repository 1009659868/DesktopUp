#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$ProtocolEvidence,
    [Parameter(Mandatory)][string]$FieldsEvidence,
    [Parameter(Mandatory)][string]$RejectedHeatEvidence,
    [Parameter(Mandatory)][string]$UiEvidence,
    [Parameter(Mandatory)][string]$LampEvidence,
    [Parameter(Mandatory)][string]$CompositionEvidence,
    [string]$DailyEvidence,
    [string]$RecipeEvidence,
    [Parameter(Mandatory)][string]$DataRoot,
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskCandidate=(Resolve-Path -LiteralPath $CandidateDirectory).Path.TrimEnd('\')
$taskData=[IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach($taskProtected in @($taskNormal,$taskCandidate)) {
    if($taskData.Equals($taskProtected,[StringComparison]::OrdinalIgnoreCase) -or
        $taskData.StartsWith($taskProtected+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Review data must be isolated from the ordinary profile and candidate.'}
}
if([IO.Path]::GetPathRoot($taskData).TrimEnd('\') -eq $taskData){throw 'A drive root cannot be review data.'}
$taskManifestPath=Join-Path $taskCandidate 'manifest.json'
$taskManifest=Get-Content -LiteralPath $taskManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($taskEntry in $taskManifest) {
    if([IO.Path]::IsPathRooted($taskEntry.path) -or $taskEntry.path -match '(^|[\\/])\.\.([\\/]|$)' -or
        $taskEntry.sha256 -notmatch '^[a-fA-F0-9]{64}$'){throw 'Invalid candidate manifest entry.'}
    $taskPath=Join-Path $taskCandidate $taskEntry.path
    if(!(Test-Path -LiteralPath $taskPath -PathType Leaf) -or
        (Get-Item -LiteralPath $taskPath).Length -ne $taskEntry.bytes -or
        (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ine $taskEntry.sha256){throw "Candidate artifact differs: $($taskEntry.path)"}
}
$taskApp=Join-Path $taskCandidate 'app'
$taskFixture=Join-Path $taskCandidate 'fixture'
$taskHash=(Get-FileHash -LiteralPath (Join-Path $taskApp 'DesktopUp.dll') -Algorithm SHA256).Hash
$taskRequiredArtifacts=@($taskManifest | Where-Object {$_.path -match '^(app|fixture)/'})
$taskActualFiles=@(Get-ChildItem -LiteralPath $taskApp,$taskFixture -File -Recurse)
if($taskRequiredArtifacts.Count -ne $taskActualFiles.Count){throw 'Candidate contains unlisted or missing runtime artifacts.'}
function Assert-ReviewEvidenceBinding([string]$taskEvidenceRoot) {
    # Windows PowerShell 5.1 emits the parsed JSON array as one pipeline object.
    # Assign first so the next pipeline enumerates files, as it also does in PowerShell 7.
    $taskEvidenceArtifacts=Get-Content -LiteralPath (Join-Path $taskEvidenceRoot 'artifacts.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $taskRows=@($taskEvidenceArtifacts | Where-Object {$_.Path -match '^(app|fixture)/'})
    if($taskRows.Count -ne $taskRequiredArtifacts.Count){
        throw "Evidence artifact set differs from the selected candidate: $taskEvidenceRoot (expected $($taskRequiredArtifacts.Count), found $($taskRows.Count))."
    }
    foreach($taskExpected in $taskRequiredArtifacts) {
        $taskMatches=@($taskRows | Where-Object Path -CEQ $taskExpected.path)
        if($taskMatches.Count -ne 1 -or $taskMatches[0].SHA256 -ine $taskExpected.sha256 -or $taskMatches[0].Length -ne $taskExpected.bytes){
            throw "Evidence artifact binding differs: $($taskExpected.path)"
        }
    }
}
$taskProtocolPairs=@(@($ProtocolEvidence,'Protocol'),@($RejectedHeatEvidence,'RejectedHeat'))
if($DailyEvidence){$taskProtocolPairs+=,@($DailyEvidence,'Daily')}
if([bool]$DailyEvidence -ne [bool]$RecipeEvidence){throw 'Supply both DailyEvidence and RecipeEvidence for the UI C feedback review.'}
foreach($taskPair in $taskProtocolPairs) {
    Assert-ReviewEvidenceBinding $taskPair[0]
    $taskEvidence=Get-Content -LiteralPath (Join-Path $taskPair[0] 'observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if(!$taskEvidence.Passed -or !$taskEvidence.ArtifactsStable -or $taskEvidence.Mode -ne $taskPair[1] -or
        $taskEvidence.ExpectedAssemblySha256 -ine $taskHash -or $taskEvidence.Runs.Count -ne 2 -or
        @($taskEvidence.Runs | Where-Object {!$_.Passed -or $_.ExitCode -ne 0 -or $_.Errors.Count -ne 0 -or $_.Cleanup.activeResources -ne 0}).Count){
        throw 'Required protocol/process-restart evidence does not match this candidate.'
    }
    if($taskPair[1] -eq 'Daily' -and $taskEvidence.ContentFeedbackDiagnostics -cne $false){
        throw 'Daily review evidence must explicitly disable test feedback.'
    }
}
$taskFields=Get-Content -LiteralPath (Join-Path $FieldsEvidence 'observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-ReviewEvidenceBinding $FieldsEvidence
if($taskFields.ExpectedAssemblySha256 -ine $taskHash -or !$taskFields.ArtifactsStable -or $taskFields.ExitCode -ne 0 -or
    $taskFields.Observation.Count -ne 1 -or !$taskFields.Observation[0].passed -or $taskFields.Errors.Count -ne 0 -or
    $taskFields.Cleanup.activeResources -ne 0 -or $taskFields.Pngs.Count -ne 4){throw 'Required field/UI evidence does not match this candidate.'}
foreach($taskExtra in @($UiEvidence,$LampEvidence)) {
    Assert-ReviewEvidenceBinding $taskExtra
    $taskObservation=Get-Content -LiteralPath (Join-Path $taskExtra 'observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    # The UI sample predates the Cleanup summary field; verify its process log directly.
    # A missing summary must never be treated as zero resources.
    $taskCleanup=@($taskObservation.Cleanup)
    if($taskExtra -eq $UiEvidence -and $null -eq $taskObservation.Cleanup) {
        $taskUiEvents=@(Get-Content -LiteralPath (Join-Path $taskExtra 'data/logs/desktopup.jsonl') -Encoding UTF8 |
            ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $taskObservation.ProcessId)
        $taskCleanup=@($taskUiEvents | Where-Object eventName -eq 'application.stopped' | ForEach-Object {$_.data})
        if(@($taskUiEvents | Where-Object level -in @('error','critical','fatal')).Count){throw 'UI process log contains errors.'}
    }
    if(!$taskObservation.ArtifactsStable -or $taskObservation.ExitCode -ne 0 -or
        $taskObservation.Observation.Count -ne 1 -or !$taskObservation.Observation[0].passed -or
        $taskObservation.Errors.Count -ne 0 -or $taskCleanup.Count -ne 1 -or $taskCleanup[0].activeResources -ne 0) {
        throw 'Required workspace or template parameter evidence does not match this candidate.'
    }
}
Assert-ReviewEvidenceBinding $CompositionEvidence
$taskComposition=Get-Content -LiteralPath (Join-Path $CompositionEvidence 'observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$taskComposition.Passed -or !$taskComposition.ArtifactsStable -or !$taskComposition.ProcessRestartTested -or
    $taskComposition.ExpectedAssemblySha256 -ine $taskHash -or $taskComposition.Runs.Count -ne 2 -or
    @($taskComposition.Runs | Where-Object {!$_.Passed -or $_.ExitCode -ne 0 -or $_.Errors.Count -ne 0 -or $_.Cleanup.activeResources -ne 0}).Count) {
    throw 'Required composition/restart evidence does not match this candidate.'
}
$taskBinding=[pscustomobject]@{CandidateDirectory=$taskCandidate;AppSha256=$taskHash;
    ManifestSha256=(Get-FileHash -LiteralPath $taskManifestPath -Algorithm SHA256).Hash;
    Scope='LR-R5 UI C alignment review; DEMO only; focused visual/intent and native input acceptance remain pending.'}
if($RecipeEvidence) {
    Assert-ReviewEvidenceBinding $RecipeEvidence
    $taskRecipes=Get-Content -LiteralPath (Join-Path $RecipeEvidence 'observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if(!$taskRecipes.Passed -or !$taskRecipes.ArtifactsStable -or !$taskRecipes.ProcessRestartTested -or
        !$taskRecipes.ActualControlHandlers -or $taskRecipes.ContentFeedbackDiagnostics -cne $false -or
        $taskRecipes.ExpectedAssemblySha256 -ine $taskHash -or $taskRecipes.Runs.Count -ne 2 -or
        @($taskRecipes.Runs | Where-Object {!$_.Passed -or $_.ExitCode -ne 0 -or $_.Errors.Count -ne 0 -or $_.Cleanup.activeResources -ne 0}).Count) {
        throw 'Required template recipe/theme restart evidence does not match this candidate.'
    }
}
if($CheckOnly){$taskBinding;return}
if(Get-Process -Name DesktopUp -ErrorAction SilentlyContinue){throw 'Exit existing DesktopUp normally first; no process stopped.'}
if(!(Test-Path -LiteralPath $taskData)) {
    & (Join-Path $taskFixture 'DesktopUp.SessionFixture.exe') $taskData 2 | Out-Null
    if($LASTEXITCODE -ne 0){throw 'New review fixture creation failed; partial data retained.'}
    $taskBinding | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskData 'review-candidate.json') -Encoding UTF8
} else {
    $taskPrevious=Get-Content -LiteralPath (Join-Path $taskData 'review-candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($taskPrevious.AppSha256 -ine $taskHash -or $taskPrevious.ManifestSha256 -ine $taskBinding.ManifestSha256 -or
        !(Test-Path -LiteralPath (Join-Path $taskData 'state\desktopup.sqlite3') -PathType Leaf)){
        throw 'Existing review profile belongs to another candidate or is incomplete; preserve it and choose a fresh DataRoot.'
    }
}
$taskArguments='--show-control-center --animation-config="'+(Join-Path $taskApp 'config\poster-animation.json')+'"'
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskApp 'DesktopUp.exe') -DataRoot $taskData -Arguments $taskArguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - UI C ALIGNMENT REVIEW PENDING';ProcessId=$taskProcess.Id;DataRoot=$taskData;
    AppSha256=$taskHash;Scope=$taskBinding.Scope}
