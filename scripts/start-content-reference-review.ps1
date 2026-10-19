#requires -Version 7
param([Parameter(Mandatory)][string]$EvidenceRoot,[Parameter(Mandatory)][string]$DataRoot,[switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$referenceRepo = Split-Path -Parent $PSScriptRoot
$referenceEvidence = (Resolve-Path -LiteralPath $EvidenceRoot).Path.TrimEnd('\')
$referenceSeal = Get-Content -LiteralPath (Join-Path $referenceEvidence 'references.json') -Raw | ConvertFrom-Json
if ($referenceSeal.Schema -ne 1 -or $referenceSeal.Result -ne 'PASS' -or $referenceSeal.ContractsPassed -lt 2541 -or
    $referenceSeal.NativeInput -ne $false -or $referenceSeal.HumanAccepted -ne $false -or
    $referenceSeal.Processes.Count -ne 2 -or @($referenceSeal.Processes | Where-Object { $_.Errors -ne 0 -or $_.ActiveResources -ne 0 }).Count) { throw 'Incomplete reference evidence.' }
$referenceCandidate = (Resolve-Path -LiteralPath $referenceSeal.Candidate).Path.TrimEnd('\')
foreach ($artifact in (Get-Content -LiteralPath (Join-Path $referenceEvidence 'artifacts.json') -Raw | ConvertFrom-Json)) {
    $path = [IO.Path]::GetFullPath((Join-Path $referenceCandidate $artifact.Path))
    if (!$path.StartsWith($referenceCandidate + '\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path).Hash -ine $artifact.SHA256) { throw 'Candidate changed.' }
}
foreach ($source in (Get-Content -LiteralPath (Join-Path $referenceEvidence 'sources.json') -Raw | ConvertFrom-Json)) {
    $path = [IO.Path]::GetFullPath((Join-Path $referenceRepo $source.Path))
    if (!$path.StartsWith($referenceRepo + '\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path).Hash -ine $source.SHA256) { throw 'Source changed; rebuild and reverify.' }
}
$referenceData = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
if (!$referenceData.StartsWith((Join-Path $referenceRepo 'out') + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $referenceData.StartsWith($referenceEvidence + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $referenceEvidence.StartsWith($referenceData + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $referenceData.StartsWith($referenceCandidate + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $referenceCandidate.StartsWith($referenceData + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $referenceData -eq $referenceEvidence -or $referenceData -eq $referenceCandidate) { throw 'Use an independent review directory under out.' }
$referenceBinding = Join-Path $referenceData 'reference-review.json'
if (Test-Path -LiteralPath $referenceData) {
    $binding = Get-Content -LiteralPath $referenceBinding -Raw | ConvertFrom-Json
    if ($binding.AppSHA256 -ine $referenceSeal.AppSHA256) { throw 'This review belongs to another candidate.' }
}
if ($CheckOnly) { [pscustomobject]@{Result='READY - NOT STARTED';DataRoot=$referenceData}; return }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally first; no process stopped.' }
if (!(Test-Path -LiteralPath $referenceData)) {
    & (Join-Path $referenceCandidate 'fixture/DesktopUp.SessionFixture.exe') $referenceData 3 --measure-expanded
    if ($LASTEXITCODE -ne 0) { throw 'Fixture failed.' }
    New-Item -ItemType Directory -Path (Join-Path $referenceData 'samples/folder') | Out-Null
    'DesktopUp non-destructive reference sample. Original stays here.' | Set-Content -LiteralPath (Join-Path $referenceData 'samples/note.txt')
    'Replacement source for the shared repair preview.' | Set-Content -LiteralPath (Join-Path $referenceData 'samples/repaired.txt')
    [pscustomobject]@{AppSHA256=$referenceSeal.AppSHA256;Scope='USER PENDING; references only, no real open/icon extraction'} | ConvertTo-Json | Set-Content -LiteralPath $referenceBinding
}
$referenceProcess = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $referenceCandidate 'app/DesktopUp.exe') -DataRoot $referenceData -Arguments '--show-control-center' -WindowStyle Hidden
[pscustomobject]@{ProcessId=$referenceProcess.Id;DataRoot=$referenceData;Samples=(Join-Path $referenceData 'samples');AppSHA256=$referenceSeal.AppSHA256;Status='USER PENDING'}
