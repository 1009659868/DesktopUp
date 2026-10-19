#requires -Version 7
param(
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256
)
$ErrorActionPreference = 'Stop'
$c1Repo = Split-Path -Parent $PSScriptRoot
$c1Candidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path.TrimEnd('\')
$c1Root = [IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')
if (!$c1Root.StartsWith((Join-Path $c1Repo 'out') + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $c1Root.StartsWith($c1Candidate + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $c1Candidate.StartsWith($c1Root + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $c1Root -eq $c1Candidate -or (Test-Path -LiteralPath $c1Root)) { throw 'Use a NEW independent project out evidence directory.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally; no process stopped.' }
$c1App = Join-Path $c1Candidate 'app'
$c1Fixture = Join-Path $c1Candidate 'fixture'
$c1Exe = Join-Path $c1App 'DesktopUp.exe'
if ((Get-FileHash -LiteralPath (Join-Path $c1App 'DesktopUp.dll')).Hash -ine $ExpectedAssemblySha256) { throw 'Wrong candidate hash.' }
function Get-C1Sources {
    foreach ($c1File in @(& git -C $c1Repo ls-files -c -o --exclude-standard | Sort-Object -Unique)) {
        if ($c1File -match '^(Docs/|\.workbuddy/|out/|\.git/)' -or $c1File -match '(^|/)(bin|obj)/' -or $c1File -match '\.md$') { continue }
        $c1Path = Join-Path $c1Repo $c1File
        if (Test-Path -LiteralPath $c1Path -PathType Leaf) { [pscustomobject]@{Path=$c1File;SHA256=(Get-FileHash -LiteralPath $c1Path).Hash} }
    }
}
function Get-C1Artifacts([string]$directory) {
    @(Get-ChildItem -LiteralPath $directory -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($directory.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$c1Sources = @(Get-C1Sources)
$c1Artifacts = @(Get-C1Artifacts $c1Candidate)
New-Item -ItemType Directory -Path $c1Root | Out-Null
$c1Sources | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $c1Root 'sources.json')
$c1Artifacts | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $c1Root 'artifacts.json')
$c1Proofs = [Collections.Generic.List[object]]::new()
function Add-C1Proof([string]$name,[string]$relative) {
    $c1Proofs.Add([pscustomobject]@{Name=$name;Path=$relative;SHA256=(Get-FileHash -LiteralPath (Join-Path $c1Root $relative)).Hash})
}
function New-C1Fixture([string]$directory,[int]$count,[string]$kind='expanded') {
    $c1Args = @($directory,"$count")
    if ($count -gt 0) { $c1Args += $(if ($kind -eq 'chain') { '--spatial-chain-r2' } else { '--measure-expanded' }) }
    & (Join-Path $c1Fixture 'DesktopUp.SessionFixture.exe') @c1Args
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed; evidence retained.' }
}
& dotnet test (Join-Path $c1Repo 'tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj') -c Release --no-restore -p:Platform=x64 --logger 'trx;LogFileName=c1-full.trx' --results-directory (Join-Path $c1Root 'contracts')
if ($LASTEXITCODE -ne 0) { throw 'Contracts failed; no seal.' }
[xml]$c1Trx = Get-Content -Raw (Join-Path $c1Root 'contracts/c1-full.trx')
$c1Counts = $c1Trx.TestRun.ResultSummary.Counters
if ([int]$c1Counts.total -lt 2424 -or $c1Counts.passed -ne $c1Counts.total) { throw 'Incomplete contracts.' }
Add-C1Proof 'Contracts' 'contracts/c1-full.trx'
foreach ($c1Count in @(0,1,3,6)) {
    $c1Data = Join-Path $c1Root "continuous-$c1Count"
    New-C1Fixture $c1Data $c1Count
    foreach ($c1Mode in @('continuous','restart')) {
        $c1Result = & (Join-Path $PSScriptRoot 'smoke-auto-lifetime.ps1') -ExecutablePath $c1Exe -DataRoot $c1Data -Mode $c1Mode
        $c1Proof = "continuous-$c1Count-$c1Mode.json"
        $c1Result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $c1Root $c1Proof)
        Add-C1Proof "Lifetime-$c1Count-$c1Mode" $c1Proof
        Write-Output "C1 $c1Count posters / $c1Mode PASS"
    }
    $c1Signals = Join-Path $c1Root "signals-$c1Count"
    New-C1Fixture $c1Signals $c1Count
    $c1Result = & (Join-Path $PSScriptRoot 'smoke-auto-lifetime.ps1') -ExecutablePath $c1Exe -DataRoot $c1Signals -Mode exercise
    $c1Proof = "signals-$c1Count.json"
    $c1Result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $c1Root $c1Proof)
    Add-C1Proof "Signals-$c1Count" $c1Proof
}
& (Join-Path $PSScriptRoot 'probe-posture-groups.ps1') -AppDirectory $c1App -FixtureDirectory $c1Fixture -OutputRoot (Join-Path $c1Root 'groups') -ExpectedAssemblySha256 $ExpectedAssemblySha256
Add-C1Proof 'Groups' 'groups/results.json'
& (Join-Path $PSScriptRoot 'probe-posture-groups.ps1') -AppDirectory $c1App -FixtureDirectory $c1Fixture -OutputRoot (Join-Path $c1Root 'combinations') -ExpectedAssemblySha256 $ExpectedAssemblySha256 -Combinations
Add-C1Proof 'Combinations' 'combinations/results.json'
$c1Surface = Join-Path $c1Root 'surface'
New-C1Fixture $c1Surface 3 'chain'
$c1Result = & (Join-Path $PSScriptRoot 'smoke-title-surface.ps1') -ExecutablePath $c1Exe -DataRoot $c1Surface
$c1Result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $c1Root 'surface.json')
Add-C1Proof 'R4Surface' 'surface.json'
Add-C1Proof 'ReviewSeed' 'continuous-3/state/desktopup.sqlite3'
if (($c1Sources | ConvertTo-Json -Compress) -cne (@(Get-C1Sources) | ConvertTo-Json -Compress)) { throw 'Sources changed; no seal.' }
if (($c1Artifacts | ConvertTo-Json -Compress) -cne (@(Get-C1Artifacts $c1Candidate) | ConvertTo-Json -Compress)) { throw 'Candidate changed; no seal.' }
$c1Processes = @(foreach ($c1Log in (Get-ChildItem -LiteralPath $c1Root -Recurse -File -Filter desktopup.jsonl)) {
    $c1Events = @(Get-Content -LiteralPath $c1Log.FullName | ForEach-Object { $_ | ConvertFrom-Json })
    foreach ($c1Group in ($c1Events | Group-Object processId)) {
        $c1Stops = @($c1Group.Group | Where-Object eventName -eq 'application.stopped')
        $c1Errors = @($c1Group.Group | Where-Object level -in @('error','critical','fatal'))
        if ($c1Stops.Count -ne 1 -or $c1Stops[0].data.activeResources -ne 0 -or $c1Errors.Count) { throw 'Process failure/resource leak; no seal.' }
        [pscustomobject]@{ProcessId=$c1Group.Name;Log=$c1Log.FullName.Substring($c1Root.Length+1);Errors=0;ActiveResources=0}
    }
})
if ($c1Processes.Count -ne 17) { throw 'Expected twelve lifetime, four group and one R4 processes.' }
$c1Processes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $c1Root 'processes.json')
Add-C1Proof 'Processes' 'processes.json'
Copy-Item -LiteralPath $c1Candidate -Destination (Join-Path $c1Root 'candidate') -Recurse
if (($c1Artifacts | ConvertTo-Json -Compress) -cne (@(Get-C1Artifacts (Join-Path $c1Root 'candidate')) | ConvertTo-Json -Compress)) { throw 'Copied artifacts changed.' }
[pscustomobject]@{Schema=1;Scope='R7-C1 bounded continuity only; real gestures/OS lifecycle/DPI/pickers remain R7-D. Former C2 deferred until basic collection complete.';
    Candidate='candidate';Sources='sources.json';Artifacts='artifacts.json';AppSHA256=$ExpectedAssemblySha256;
    Proofs=$c1Proofs.ToArray();ContractsPassed=[int]$c1Counts.passed;ProcessCount=$c1Processes.Count;
    RoundsPerCatalog=20;CatalogCounts=@(0,1,3,6);NativeInput=$false;LongRun=$false;ManualAccepted=$false} |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $c1Root 'r7-continuity.json')
[pscustomobject]@{Result='R7-C1 AUTO COMPLETE - R7-D PENDING';Contracts=$c1Counts.passed;Processes=$c1Processes.Count;Root=$c1Root}
