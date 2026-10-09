#requires -Version 7
param([Parameter(Mandatory)][string]$CandidateDirectory, [Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$referenceRepo = Split-Path -Parent $PSScriptRoot
$referenceCandidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path.TrimEnd('\')
$referenceEvidence = [IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')
$referenceAllowed = (Join-Path $referenceRepo 'out') + '\'
if (!$referenceEvidence.StartsWith($referenceAllowed, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $referenceEvidence) -or $referenceEvidence.StartsWith($referenceCandidate + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $referenceCandidate.StartsWith($referenceEvidence + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a NEW independent project out evidence directory.' }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first; no process stopped.' }
function Get-ReferenceArtifacts {
    @(Get-ChildItem -LiteralPath $referenceCandidate -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($referenceCandidate.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$referenceArtifacts = @(Get-ReferenceArtifacts)
$referenceSources = @(foreach ($relative in @(& git -C $referenceRepo ls-files -c -o --exclude-standard | Sort-Object -Unique)) {
    if ($relative -notmatch '^(src/|tests/|scripts/)' -or $relative -match '/(bin|obj|TestResults)/') { continue }
    $source = Join-Path $referenceRepo $relative
    if (Test-Path -LiteralPath $source -PathType Leaf) { [pscustomobject]@{Path=$relative;SHA256=(Get-FileHash -LiteralPath $source).Hash} }
})
New-Item -ItemType Directory -Path $referenceEvidence | Out-Null
$referenceArtifacts | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $referenceEvidence 'artifacts.json')
$referenceSources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $referenceEvidence 'sources.json')
& dotnet test (Join-Path $referenceRepo 'tests/DesktopUp.Contracts.Tests/DesktopUp.Contracts.Tests.csproj') -c Release --no-restore -p:Platform=x64 --logger 'trx;LogFileName=contracts.trx' --results-directory (Join-Path $referenceEvidence 'contracts')
if ($LASTEXITCODE -ne 0) { throw 'Contract failures preserved; no PASS seal.' }
$referenceData = Join-Path $referenceEvidence 'data'
& (Join-Path $referenceCandidate 'fixture/DesktopUp.SessionFixture.exe') $referenceData 3 --measure-expanded
if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
$referenceProcesses = @()
foreach ($phase in @('add','restart')) {
    $referenceProcess = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $referenceCandidate 'app/DesktopUp.exe') -DataRoot $referenceData -Arguments '--content-reference-probe --exit-after-ms=50000' -WindowStyle Hidden
    if (!$referenceProcess.WaitForExit(55000)) { throw "Process remains running: PID $($referenceProcess.Id). No force termination." }
    $events = @(Get-Content -LiteralPath (Join-Path $referenceData 'logs/desktopup.jsonl') | ConvertFrom-Json | Where-Object processId -eq $referenceProcess.Id)
    $done = @($events | Where-Object eventName -eq 'content.references_observed')
    $stops = @($events | Where-Object eventName -eq 'application.stopped')
    $errors = @($events | Where-Object level -in 'error','critical','fatal')
    if ($referenceProcess.ExitCode -ne 0 -or $done.Count -ne 1 -or !$done[0].data.passed -or
        $done[0].data.restart -ne ($phase -eq 'restart') -or $errors.Count -ne 0 -or $stops.Count -ne 1 -or $stops[0].data.activeResources -ne 0) { throw "Reference $phase failed; evidence retained." }
    $referenceProcesses += [pscustomobject]@{Phase=$phase;ProcessId=$referenceProcess.Id;ExitCode=$referenceProcess.ExitCode;Checks=$done[0].data.checks;Errors=0;ActiveResources=0}
}
if (($referenceArtifacts | ConvertTo-Json -Compress) -cne (@(Get-ReferenceArtifacts) | ConvertTo-Json -Compress)) { throw 'Artifacts changed during verification.' }
foreach ($source in $referenceSources) {
    if ((Get-FileHash -LiteralPath (Join-Path $referenceRepo $source.Path)).Hash -ine $source.SHA256) { throw 'Source changed during verification.' }
}
[xml]$trx = Get-Content -LiteralPath (Join-Path $referenceEvidence 'contracts/contracts.trx') -Raw
$counts = $trx.TestRun.ResultSummary.Counters
if ([int]$counts.total -lt 2541 -or $counts.total -ne $counts.passed) { throw 'Incomplete contracts.' }
$referenceResult = [pscustomobject]@{Schema=1;Result='PASS';Candidate=$referenceCandidate;
    AppSHA256=(Get-FileHash -LiteralPath (Join-Path $referenceCandidate 'app/DesktopUp.dll')).Hash;
    ContractsPassed=[int]$counts.passed;Processes=$referenceProcesses;NativeInput=$false;HumanAccepted=$false;ShellExecuted=$false;UserFiles=$false}
$referenceResult | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $referenceEvidence 'references.json')
$referenceResult
