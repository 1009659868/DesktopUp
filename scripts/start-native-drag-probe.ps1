#requires -Version 5.1
param([switch]$PrepareOnly, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
if ($PrepareOnly -and $CheckOnly) { throw 'Choose PrepareOnly OR CheckOnly.' }
$d1Repo = Split-Path -Parent $PSScriptRoot
$d1ProofPath = Join-Path $d1Repo 'out/drop-d1/proof-permission01.json'
$d1Proof = Get-Content -LiteralPath $d1ProofPath -Raw | ConvertFrom-Json
if ($d1Proof.Result -ne 'PROBE_AUTO_PASS_NATIVE_PENDING') { throw 'D1 verified probe required; this is not a product release.' }
$d1Package = [IO.Path]::GetFullPath((Join-Path $d1Repo $d1Proof.Candidate))
if (!$d1Package.StartsWith($d1Repo+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid candidate path.' }
foreach ($d1Source in $d1Proof.Sources) {
    $d1Path = [IO.Path]::GetFullPath((Join-Path $d1Repo $d1Source.Path))
    if (!$d1Path.StartsWith($d1Repo+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $d1Path).Hash -ne $d1Source.SHA256) { throw 'Probe sources changed; create a new verified candidate, do not rebind old proof.' }
}
foreach ($d1Artifact in $d1Proof.Artifacts) {
    $d1Path = [IO.Path]::GetFullPath((Join-Path $d1Package $d1Artifact.Path))
    if (!$d1Path.StartsWith($d1Package+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $d1Path).Hash -ne $d1Artifact.SHA256) { throw 'Probe package changed.' }
}
$d1Review = Join-Path $d1Repo ('out/drop-d1/review-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $d1Review | Out-Null
New-Item -ItemType File -Path (Join-Path $d1Review 'd1-probe-owned.marker') | Out-Null
$d1Fixtures = Join-Path $d1Review 'external-source'
New-Item -ItemType Directory -Path $d1Fixtures | Out-Null
'D1 self-created disposable payload. The receiving probe never moves this file.' | Set-Content -LiteralPath (Join-Path $d1Fixtures 'D1-test.txt') -Encoding UTF8
if ($PrepareOnly) { [pscustomobject]@{Prepared=$true;Started=$false;Review=$d1Review;Package=$d1Package}; return }
$d1Exe = Join-Path $d1Package 'DesktopUp.NativeDragProbe.exe'
# Use the very same read-only policy as target/source startup; never alter tokens or UIPI.
$d1Check = Start-Process -FilePath $d1Exe -ArgumentList @('--check-environment',('"'+$d1Review+'"')) -WindowStyle Hidden -PassThru
if (!$d1Check.WaitForExit(15000)) { throw "D1 environment check timed out. See $d1Review. No interactive probe started." }
$d1EnvironmentPath = Join-Path $d1Review 'environment.json'
if (!(Test-Path -LiteralPath $d1EnvironmentPath -PathType Leaf)) { throw "D1 environment check did not produce a report. See $d1Review." }
$d1Environment = Get-Content -LiteralPath $d1EnvironmentPath -Raw | ConvertFrom-Json
if ($d1Check.ExitCode -ne 0 -or !$d1Environment.Allowed) {
    throw "D1 cannot verify compatible desktop Explorer tokens: $($d1Environment.Reason). Require the same user, session and integrity level. Do not change UAC or bypass UIPI. Report: $d1EnvironmentPath"
}
if ($d1Environment.Kind -eq 'HighIntegrityMatched') {
    Write-Warning 'Explorer and probe both use High integrity. Local dry-run is allowed; this does NOT validate ordinary/Medium-privilege drag-and-drop. UAC and tokens are unchanged.'
}
if ($CheckOnly) { [pscustomobject]@{Prepared=$true;Started=$false;Environment=$d1Environment.Kind;Review=$d1Review;Package=$d1Package}; return }
# User-visible interactive test application; not a background helper.
$d1Process = Start-Process -FilePath $d1Exe -ArgumentList @('--target',('"'+$d1Review+'"')) -WindowStyle Normal -PassThru
[pscustomobject]@{ProcessId=$d1Process.Id;Environment=$d1Environment.Kind;Review=$d1Review;TestFile=(Join-Path $d1Fixtures 'D1-test.txt');Scope='Native dry-run only. No file moves or application configuration writes.'}
