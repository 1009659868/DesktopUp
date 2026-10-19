#requires -Version 5.1
# Isolated R1 storage review. Reuses the owned file/folder fixture; never installs or uses desktop samples.
param([ValidateSet('Open','Prepare','Status')][string]$Action='Open')
$ErrorActionPreference='Stop'
$r1Repo=Split-Path -Parent $PSScriptRoot
$r1Candidate=Join-Path $r1Repo 'out/architecture-r1-storage/candidate01'
$r1Product=Join-Path $r1Candidate 'app'
$r1Review=Join-Path $r1Repo 'out/drop-d3/architecture-r1-storage-review01'
$r1Profile=Join-Path $r1Review 'profile'
$r1Fixture=Join-Path $r1Candidate 'fixture/DesktopUp.SessionFixture.exe'
$r1App=Join-Path $r1Product 'DesktopUp.exe'
$r1Ready=Join-Path $r1Review '.r1-storage-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Executable=$r1App;Profile=$r1Profile;Samples=$r1Review;Prepared=(Test-Path -LiteralPath $r1Ready)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. Nothing stopped.'}
foreach($r1Path in @($r1App,$r1Fixture,(Join-Path $r1Product 'content-worker/DesktopUp.ContentWorker.exe'))){
    if(!(Test-Path -LiteralPath $r1Path -PathType Leaf)){throw "Missing R1 build: $r1Path"}
}
$r1Hashes=@{
    'DesktopUp.dll'='6275703FE1E13CCF434BA16A2679FE5D2533E0DD45B43E900F8F198521B8838A'
    'DesktopUp.Application.dll'='0E2E671D1851057A235303DD530C362ED4A020FC2C84DC53220C0EF8FE995793'
    'DesktopUp.Infrastructure.dll'='D6786B432F07D6BBC5648FBF61B8FC792B777FCE15C70F59E2B4E19D52C96C1D'
}
foreach($r1Name in $r1Hashes.Keys){
    if((Get-FileHash -LiteralPath (Join-Path $r1Product $r1Name)).Hash -cne $r1Hashes[$r1Name]){
        throw "R1 candidate changed: $r1Name. Do not reuse the previous acceptance record."
    }
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$r1Tools=Enter-DesktopUpToolchain -Offline
try {
    if(!(Test-Path -LiteralPath $r1Review)){
        New-Item -ItemType Directory -Path $r1Review | Out-Null
        [IO.File]::WriteAllText((Join-Path $r1Review '.uninstall-fixture'),'owned')
        & $r1Fixture $r1Profile --architecture-storage | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Fixture preparation failed; evidence preserved, no overwrite.'}
        foreach($r1Name in @('empty-target-A','empty-target-B','occupied-target')){
            New-Item -ItemType Directory -Path (Join-Path $r1Review $r1Name) | Out-Null
        }
        [IO.File]::WriteAllText((Join-Path $r1Review 'occupied-target/keep.txt'),'Owned occupied-target sentinel; preserve it.')
        $r1Samples=@(Get-ChildItem -LiteralPath (Join-Path $r1Review 'storage'),(Join-Path $r1Review 'occupied-target') -Recurse -File |
            ForEach-Object { [pscustomobject]@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
        $r1Samples | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $r1Review 'sample-manifest.json') -Encoding UTF8
        [IO.File]::WriteAllText($r1Ready,'owned')
    }
    if(!(Test-Path -LiteralPath $r1Ready) -or [IO.File]::ReadAllText($r1Ready) -ne 'owned'){
        throw 'Not a completed owned R1 fixture. Nothing overwritten.'
    }
    if($Action -eq 'Prepare'){
        [pscustomobject]@{Profile=$r1Profile;Samples=$r1Review;Executable=$r1App}; return
    }
    $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$r1Process.Id;Profile=$r1Profile;Samples=$r1Review;InstallerExecuted=$false}
} finally {Exit-DesktopUpToolchain $r1Tools}
