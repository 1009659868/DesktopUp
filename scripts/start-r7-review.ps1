#requires -Version 7
param([Parameter(Mandatory)][string]$EvidenceRoot, [Parameter(Mandatory)][string]$DataRoot, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$reviewRoot = (Resolve-Path -LiteralPath $EvidenceRoot).Path.TrimEnd('\')
function Resolve-R7Proof([string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)) { throw 'Expected a relative sealed path.' }
    $path = [IO.Path]::GetFullPath((Join-Path $reviewRoot $relative))
    if (!$path.StartsWith($reviewRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence path escaped root.' }
    return $path
}
$sealPath = Join-Path $reviewRoot 'r7-continuity.json'
$seal = Get-Content -LiteralPath $sealPath -Raw | ConvertFrom-Json
if ($seal.Schema -ne 1 -or $seal.ProcessCount -ne 17 -or $seal.ContractsPassed -lt 2424 -or
    $seal.RoundsPerCatalog -ne 20 -or (@($seal.CatalogCounts) -join ',') -ne '0,1,3,6' -or
    $seal.NativeInput -ne $false -or $seal.LongRun -ne $false -or $seal.ManualAccepted -ne $false) { throw 'Incomplete C1 seal.' }
$required = @('Contracts','Groups','Combinations','R4Surface','ReviewSeed','Processes')
foreach ($count in @(0,1,3,6)) { $required += @("Lifetime-$count-continuous","Lifetime-$count-restart","Signals-$count") }
if (@($seal.Proofs).Count -ne $required.Count -or
    (Compare-Object ($required | Sort-Object) @($seal.Proofs.Name | Sort-Object))) { throw 'Required C1 proofs missing/duplicated.' }
foreach ($proof in $seal.Proofs) {
    if ($proof.SHA256 -notmatch '^[a-fA-F0-9]{64}$' -or
        (Get-FileHash -LiteralPath (Resolve-R7Proof $proof.Path)).Hash -ine $proof.SHA256) { throw 'C1 proof changed.' }
}
$candidate = Resolve-R7Proof $seal.Candidate
$artifacts = @(Get-Content -LiteralPath (Resolve-R7Proof $seal.Artifacts) -Raw | ConvertFrom-Json)
$actual = @(Get-ChildItem -LiteralPath $candidate -Recurse -File | ForEach-Object { $_.FullName.Substring($candidate.Length+1).Replace('\','/') } | Sort-Object)
if (Compare-Object $actual @($artifacts.Path | Sort-Object)) { throw 'Runtime file set changed.' }
foreach ($artifact in $artifacts) {
    if ((Get-FileHash -LiteralPath (Resolve-R7Proof ($seal.Candidate + '/' + $artifact.Path))).Hash -ine $artifact.SHA256) { throw 'Runtime artifact changed.' }
}
$appHash = (Get-FileHash -LiteralPath (Join-Path $candidate 'app/DesktopUp.dll')).Hash
if ($appHash -ine $seal.AppSHA256) { throw 'Wrong selected assembly.' }
$sources = @(Get-Content -LiteralPath (Resolve-R7Proof $seal.Sources) -Raw | ConvertFrom-Json)
$repo = Split-Path -Parent $PSScriptRoot
foreach ($source in $sources) {
    $path = [IO.Path]::GetFullPath((Join-Path $repo $source.Path))
    if (!$path.StartsWith($repo + '\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $path).Hash -ine $source.SHA256) { throw 'C1 source no longer matches the candidate.' }
}
$processProof = $seal.Proofs | Where-Object Name -eq 'Processes'
$processes = @(Get-Content -LiteralPath (Resolve-R7Proof $processProof.Path) -Raw | ConvertFrom-Json)
if ($processes.Count -ne 17 -or @($processes | Where-Object { $_.Errors -ne 0 -or $_.ActiveResources -ne 0 }).Count) { throw 'Unclean C1 process evidence.' }
$surfaceProof = $seal.Proofs | Where-Object Name -eq 'R4Surface'
$surface = Get-Content -LiteralPath (Resolve-R7Proof $surfaceProof.Path) -Raw | ConvertFrom-Json
if ($surface.Result -ne 'PASS' -or $surface.Cases.Count -ne 12 -or $surface.ActiveResourcesAfterExit -ne 0 -or
    'final-restore-region-reset' -notin $surface.Cases) { throw 'Final-restore region fault gate is missing.' }
$reviewData = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\')
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'out')).TrimEnd('\') + '\'
if (!$reviewData.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or
    $reviewData.StartsWith($reviewRoot + '\',[StringComparison]::OrdinalIgnoreCase) -or
    $reviewRoot.StartsWith($reviewData + '\',[StringComparison]::OrdinalIgnoreCase) -or $reviewData -eq $reviewRoot) {
    throw 'Use an independent review directory under out, outside sealed evidence.'
}
$binding = [pscustomobject]@{AppSHA256=$appHash;SealSHA256=(Get-FileHash -LiteralPath $sealPath).Hash;
    Scope='R7-D USER PENDING; mixed Rain/Lamp/minimal DEMO, no user files; C2 deferred, not passed.'}
if (Test-Path -LiteralPath $reviewData) {
    $previous = Get-Content -LiteralPath (Join-Path $reviewData 'r7-review-binding.json') -Raw | ConvertFrom-Json
    if ($previous.AppSHA256 -ine $appHash -or $previous.SealSHA256 -ine $binding.SealSHA256) { throw 'Existing review belongs to another candidate.' }
}
if ($CheckOnly) { [pscustomobject]@{Result='READY - NOT STARTED';DataRoot=$reviewData;Binding=$binding}; return }
if (Get-Process DesktopUp -ErrorAction SilentlyContinue) { throw 'Exit DesktopUp normally; no process stopped.' }
if (!(Test-Path -LiteralPath $reviewData)) {
    $seed = $seal.Proofs | Where-Object Name -eq 'ReviewSeed'
    # Copy only a normally closed fixture database, never mutate the sealed evidence.
    $seedPath = Resolve-R7Proof $seed.Path
    if ((Test-Path -LiteralPath ($seedPath + '-wal')) -or (Test-Path -LiteralPath ($seedPath + '-shm'))) { throw 'Review seed is not checkpointed/closed.' }
    New-Item -ItemType Directory -Path (Join-Path $reviewData 'state') | Out-Null
    Copy-Item -LiteralPath $seedPath -Destination (Join-Path $reviewData 'state/desktopup.sqlite3')
    $binding | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reviewData 'r7-review-binding.json')
}
$arguments = '--show-control-center --animation-config="' + (Join-Path $candidate 'app/config/poster-animation.json') + '"'
$process = Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $candidate 'app/DesktopUp.exe') -DataRoot $reviewData -Arguments $arguments -WindowStyle Hidden
[pscustomobject]@{Result='STARTED - R7-D USER PENDING';ProcessId=$process.Id;DataRoot=$reviewData;Binding=$binding}
