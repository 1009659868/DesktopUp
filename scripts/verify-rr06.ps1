#requires -Version 7
param(
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256
)
$ErrorActionPreference = 'Stop'
$rrRepo = Split-Path -Parent $PSScriptRoot
$rrCandidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path.TrimEnd('\')
$rrRoot = [IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')
$rrAllowed = (Join-Path $rrRepo 'out') + '\'
if (!$rrRoot.StartsWith($rrAllowed,[StringComparison]::OrdinalIgnoreCase) -or
    $rrRoot.StartsWith($rrCandidate + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $rrRoot -eq $rrCandidate -or (Test-Path -LiteralPath $rrRoot)) { throw 'Use a NEW project out evidence root outside candidate.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first; no process stopped.' }
$rrApp = Join-Path $rrCandidate 'app'
$rrFixture = Join-Path $rrCandidate 'fixture'
$rrModule = Join-Path $rrCandidate 'module-fixture'
if ((Get-FileHash -LiteralPath (Join-Path $rrApp 'DesktopUp.dll')).Hash -ine $ExpectedAssemblySha256) { throw 'Candidate hash mismatch.' }
function Get-RrSources {
    foreach ($rrRelative in @(& git -C $rrRepo ls-files -c -o --exclude-standard | Sort-Object -Unique)) {
        if ($rrRelative -match '^(Docs/|\.workbuddy/|out/|\.git/)' -or $rrRelative -match '(^|/)(bin|obj)/' -or $rrRelative -match '\.md$') { continue }
        $rrPath = Join-Path $rrRepo $rrRelative
        if (Test-Path -LiteralPath $rrPath -PathType Leaf) {
            [pscustomobject]@{Path=$rrRelative;SHA256=(Get-FileHash -LiteralPath $rrPath).Hash}
        }
    }
}
function Get-RrArtifacts([string]$rrDirectory) {
    @(Get-ChildItem -LiteralPath $rrDirectory -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($rrDirectory.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$rrBefore = @(Get-RrArtifacts $rrCandidate)
$rrSources = @(Get-RrSources)
New-Item -ItemType Directory -Path $rrRoot | Out-Null
$rrSources | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rrRoot 'sources.json')
$rrBefore | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rrRoot 'artifacts.json')
$rrProofs = [Collections.Generic.List[object]]::new()
function Add-RrProof([string]$name,[string]$relative) {
    $rrProofs.Add([pscustomobject]@{Name=$name;Path=$relative;SHA256=(Get-FileHash -LiteralPath (Join-Path $rrRoot $relative)).Hash})
}
function Run-RrGate([string]$name,[string]$script,[string]$folder,[string]$proof,[hashtable]$extra=@{}) {
    Write-Output "RR06: $name"
    & (Join-Path $PSScriptRoot $script) -AppDirectory $rrApp -FixtureDirectory $rrFixture -OutputRoot (Join-Path $rrRoot $folder) -ExpectedAssemblySha256 $ExpectedAssemblySha256 @extra
    Add-RrProof $name ($folder+'/'+$proof)
}
$rrContractRoot = Join-Path $rrRoot 'contracts'
& dotnet test (Join-Path $rrRepo 'tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj') -c Release --no-restore -p:Platform=x64 --logger 'trx;LogFileName=rr06-full.trx' --results-directory $rrContractRoot
if ($LASTEXITCODE -ne 0) { throw 'Contract suite failed; no seal created.' }
[xml]$rrTrx = Get-Content -Raw (Join-Path $rrContractRoot 'rr06-full.trx')
$rrCounts = $rrTrx.TestRun.ResultSummary.Counters
if ([int]$rrCounts.total -lt 2274 -or $rrCounts.total -ne $rrCounts.passed) { throw 'Incomplete contract run.' }
Add-RrProof 'Contracts' 'contracts/rr06-full.trx'
Run-RrGate 'Groups' 'probe-posture-groups.ps1' 'groups' 'results.json'
Run-RrGate 'Combinations' 'probe-posture-groups.ps1' 'combinations' 'results.json' @{Combinations=$true}
Run-RrGate 'Rain' 'probe-rain-scene.ps1' 'rain' 'results.json'
Run-RrGate 'Resources' 'probe-rain-scene.ps1' 'resources' 'results.json' @{ResourceBaseline=$true}
Run-RrGate 'Lamp' 'probe-lamp-parameters.ps1' 'lamp' 'observation.json'
Run-RrGate 'Recipes' 'probe-template-recipes.ps1' 'recipes' 'observation.json'
Run-RrGate 'Composition' 'probe-content-composition.ps1' 'composition' 'observation.json'
Run-RrGate 'Browse' 'probe-content-browse.ps1' 'browse' 'observation.json'
Write-Output 'RR06: Layout'
& (Join-Path $PSScriptRoot 'smoke-content-layout.ps1') -AppDirectory $rrApp -FixtureDirectory $rrFixture -OutputRoot (Join-Path $rrRoot 'layout')
Add-RrProof 'Layout' 'layout/content-layout-gates-passed.json'
$rrR4 = Join-Path $rrRoot 'r4'
& (Join-Path $rrFixture 'DesktopUp.SessionFixture.exe') $rrR4 3 --spatial-chain-r2
if ($LASTEXITCODE -ne 0) { throw 'R4 fixture failed.' }
$rrR4Result = & (Join-Path $PSScriptRoot 'smoke-title-surface.ps1') -ExecutablePath (Join-Path $rrApp 'DesktopUp.exe') -DataRoot $rrR4
$rrR4Result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $rrR4 'result.json')
Add-RrProof 'R4' 'r4/result.json'
$rrSpatial = @()
foreach ($rrCase in @('single','chain')) {
    $rrData = Join-Path $rrRoot ('spatial-'+$rrCase)
    $rrArgs = if ($rrCase -eq 'single') { @($rrData,'1') } else { @($rrData,'3','--spatial-chain-r2') }
    & (Join-Path $rrFixture 'DesktopUp.SessionFixture.exe') @rrArgs
    if ($LASTEXITCODE -ne 0) { throw 'Spatial fixture failed.' }
    $rrSpatial += & (Join-Path $PSScriptRoot 'smoke-spatial.ps1') -ExecutablePath (Join-Path $rrApp 'DesktopUp.exe') -DataRoot $rrData
}
$rrSpatial | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $rrRoot 'spatial.json')
Add-RrProof 'Spatial' 'spatial.json'
foreach ($rrModuleName in @('Lamp','Rain')) {
    Write-Output "RR06: Remove/restore $rrModuleName"
    & (Join-Path $PSScriptRoot 'verify-scene-module-removal.ps1') -AppDirectory $rrApp -ModuleFixtureDirectory $rrModule -OutputRoot (Join-Path $rrRoot ('remove-'+$rrModuleName)) -ExpectedAssemblySha256 $ExpectedAssemblySha256 -Module $rrModuleName
    Add-RrProof ('Remove'+$rrModuleName) ('remove-'+$rrModuleName+'/results.json')
    $rrSeed = Join-Path $rrRoot ('animation-seed-'+$rrModuleName)
    & (Join-Path $rrModule 'DesktopUp.SceneModuleFixture.exe') seed $rrSeed $rrModuleName.ToLowerInvariant()
    if ($LASTEXITCODE -ne 0) { throw 'Animation fixture failed.' }
    & (Join-Path $PSScriptRoot 'probe-posture-live.ps1') -AppDirectory $rrApp -DatabaseCopy (Join-Path $rrSeed 'state/desktopup.sqlite3') -OutputRoot (Join-Path $rrRoot ('animation-'+$rrModuleName)) -ExpectedAssemblySha256 $ExpectedAssemblySha256
    Add-RrProof ('Animation'+$rrModuleName) ('animation-'+$rrModuleName+'/observation.json')
}
# Candidate and sources are immutable throughout the suite. Preserve originals; copy verified
# runtime artifacts under the seal root so the existing strict human entry cannot escape it.
if (($rrBefore | ConvertTo-Json -Compress) -cne (@(Get-RrArtifacts $rrCandidate) | ConvertTo-Json -Compress)) { throw 'Candidate changed during suite.' }
if (($rrSources | ConvertTo-Json -Compress) -cne (@(Get-RrSources) | ConvertTo-Json -Compress)) { throw 'Sources changed during suite.' }
Copy-Item -LiteralPath $rrCandidate -Destination (Join-Path $rrRoot 'candidate') -Recurse
if (($rrBefore | ConvertTo-Json -Compress) -cne (@(Get-RrArtifacts (Join-Path $rrRoot 'candidate')) | ConvertTo-Json -Compress)) { throw 'Copied candidate differs.' }
$rrProcesses = foreach ($rrLog in (Get-ChildItem -LiteralPath $rrRoot -Filter desktopup.jsonl -Recurse -File)) {
    $rrEvents = @(Get-Content -LiteralPath $rrLog.FullName | ForEach-Object { $_ | ConvertFrom-Json })
    foreach ($rrGroup in ($rrEvents | Group-Object processId)) {
        $rrStop = @($rrGroup.Group | Where-Object eventName -eq 'application.stopped')
        $rrErrors = @($rrGroup.Group | Where-Object level -in @('error','critical','fatal'))
        if ($rrStop.Count -ne 1 -or $rrStop[0].data.activeResources -ne 0 -or $rrErrors.Count) { throw 'Process error or resource leak; no final seal.' }
        [pscustomobject]@{ProcessId=$rrGroup.Name;Log=$rrLog.FullName.Substring($rrRoot.Length+1);ActiveResources=0;Errors=0}
    }
}
$rrProcesses | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $rrRoot 'processes.json')
$rrSeal = [pscustomobject]@{Schema=2;Candidate='candidate';Artifacts='artifacts.json';Sources='sources.json';AppSHA256=$ExpectedAssemblySha256;
    Proofs=$rrProofs.ToArray();ContractsPassed=[int]$rrCounts.passed;ProcessCount=@($rrProcesses).Count;IncludesRemovedModuleVariants=4;
    Scope='Reusable RR06 regression baseline, including R7-0 reliability guards. NOT complete R7: portable fidelity, long resource tests, native input, desktop composition and USER acceptance PENDING.';
    Deferred=@('BUG-031 / RR03: user deferred, NOT fixed');ManualAccepted=$false}
$rrSeal | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $rrRoot 'rain-review.json')
[pscustomobject]@{Result='AUTO GATES COMPLETE - USER PENDING';Root=$rrRoot;Contracts=$rrCounts.passed;Processes=@($rrProcesses).Count;Gates=$rrProofs.Count}
