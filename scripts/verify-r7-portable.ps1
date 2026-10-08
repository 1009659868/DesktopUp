#requires -Version 7
param(
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256
)
$ErrorActionPreference = 'Stop'
$r7Repo = Split-Path -Parent $PSScriptRoot
$r7Candidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path.TrimEnd('\')
$r7Root = [IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')
if (!$r7Root.StartsWith((Join-Path $r7Repo 'out') + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $r7Root.StartsWith($r7Candidate + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $r7Candidate.StartsWith($r7Root + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $r7Root -eq $r7Candidate -or (Test-Path -LiteralPath $r7Root)) { throw 'Use a NEW independent project out evidence root.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first; no process stopped.' }
$r7App = Join-Path $r7Candidate 'app'
$r7Fixture = Join-Path $r7Candidate 'fixture'
$r7Module = Join-Path $r7Candidate 'module-fixture'
if ((Get-FileHash -LiteralPath (Join-Path $r7App 'DesktopUp.dll')).Hash -ine $ExpectedAssemblySha256) { throw 'Wrong candidate.' }
function Get-R7Sources {
    foreach ($r7File in @(& git -C $r7Repo ls-files -c -o --exclude-standard | Sort-Object -Unique)) {
        if ($r7File -match '^(Docs/|\.workbuddy/|out/|\.git/)' -or $r7File -match '(^|/)(bin|obj)/' -or $r7File -match '\.md$') { continue }
        $r7Path = Join-Path $r7Repo $r7File
        if (Test-Path -LiteralPath $r7Path -PathType Leaf) { [pscustomobject]@{Path=$r7File;SHA256=(Get-FileHash -LiteralPath $r7Path).Hash} }
    }
}
function Get-R7Artifacts([string]$directory) {
    @(Get-ChildItem -LiteralPath $directory -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($directory.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$r7Sources = @(Get-R7Sources)
$r7Artifacts = @(Get-R7Artifacts $r7Candidate)
New-Item -ItemType Directory -Path $r7Root | Out-Null
$r7Sources | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $r7Root 'sources.json')
$r7Artifacts | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $r7Root 'artifacts.json')
$r7Proofs = [Collections.Generic.List[object]]::new()
function Add-R7Proof([string]$name,[string]$relative) {
    $r7Proofs.Add([pscustomobject]@{Name=$name;Path=$relative;SHA256=(Get-FileHash -LiteralPath (Join-Path $r7Root $relative)).Hash})
}
& dotnet test (Join-Path $r7Repo 'tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj') -c Release --no-restore -p:Platform=x64 --logger 'trx;LogFileName=r7-full.trx' --results-directory (Join-Path $r7Root 'contracts')
if ($LASTEXITCODE -ne 0) { throw 'Contracts failed; no seal.' }
[xml]$r7Trx = Get-Content -Raw (Join-Path $r7Root 'contracts/r7-full.trx')
$r7Counts = $r7Trx.TestRun.ResultSummary.Counters
if ([int]$r7Counts.total -lt 2396 -or $r7Counts.passed -ne $r7Counts.total) { throw 'Incomplete contracts.' }
Add-R7Proof 'Contracts' 'contracts/r7-full.trx'
& (Join-Path $PSScriptRoot 'smoke-portable-profile.ps1') -AppDirectory $r7App -FixtureDirectory $r7Fixture -OutputRoot (Join-Path $r7Root 'portable') -FullConfiguration
Add-R7Proof 'PortableFull' 'portable/portable-gates-passed.json'
foreach ($r7ModuleName in @('Lamp','Rain')) {
    & (Join-Path $PSScriptRoot 'verify-scene-module-removal.ps1') -AppDirectory $r7App -ModuleFixtureDirectory $r7Module -OutputRoot (Join-Path $r7Root ('remove-'+$r7ModuleName)) -ExpectedAssemblySha256 $ExpectedAssemblySha256 -Module $r7ModuleName
    Add-R7Proof ('Remove'+$r7ModuleName) ('remove-'+$r7ModuleName+'/results.json')
}
if (($r7Sources | ConvertTo-Json -Compress) -cne (@(Get-R7Sources) | ConvertTo-Json -Compress)) { throw 'Sources changed; no seal.' }
if (($r7Artifacts | ConvertTo-Json -Compress) -cne (@(Get-R7Artifacts $r7Candidate) | ConvertTo-Json -Compress)) { throw 'Candidate changed; no seal.' }
$r7Processes = @(foreach ($r7Log in (Get-ChildItem -LiteralPath $r7Root -Recurse -File -Filter desktopup.jsonl)) {
    $r7Events = @(Get-Content -LiteralPath $r7Log.FullName | ForEach-Object { $_ | ConvertFrom-Json })
    foreach ($r7Group in ($r7Events | Group-Object processId)) {
        $r7Stops = @($r7Group.Group | Where-Object eventName -eq 'application.stopped')
        $r7Errors = @($r7Group.Group | Where-Object level -in @('error','critical','fatal'))
        if ($r7Stops.Count -ne 1 -or $r7Stops[0].data.activeResources -ne 0 -or $r7Errors.Count) { throw 'Process failure/resource leak; no seal.' }
        [pscustomobject]@{ProcessId=$r7Group.Name;Log=$r7Log.FullName.Substring($r7Root.Length+1);Errors=0;ActiveResources=0}
    }
})
if ($r7Processes.Count -ne 14) { throw 'Expected six full-portable and eight module lifecycle processes.' }
$r7Processes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $r7Root 'processes.json')
Copy-Item -LiteralPath $r7Candidate -Destination (Join-Path $r7Root 'candidate') -Recurse
if (($r7Artifacts | ConvertTo-Json -Compress) -cne (@(Get-R7Artifacts (Join-Path $r7Root 'candidate')) | ConvertTo-Json -Compress)) { throw 'Copied artifacts changed.' }
[pscustomobject]@{Schema=1;Scope='R7-A/B configuration AUTO only; R7-C/D long resources, real monitor/DPI, native picker/input and USER acceptance pending.';
    Candidate='candidate';Sources='sources.json';Artifacts='artifacts.json';AppSHA256=$ExpectedAssemblySha256;
    Proofs=$r7Proofs.ToArray();ContractsPassed=[int]$r7Counts.passed;ProcessCount=$r7Processes.Count;ManualAccepted=$false} |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $r7Root 'r7-portable.json')
[pscustomobject]@{Result='R7-A/B AUTO COMPLETE - USER PENDING';Contracts=$r7Counts.passed;Processes=$r7Processes.Count;Root=$r7Root}
