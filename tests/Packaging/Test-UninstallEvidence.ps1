#requires -Version 5.1
param([ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Name='uninstall-evidence-unit01')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$root=Join-Path $repo ('out/releases/'+$Name)
if(Test-Path -LiteralPath $root){throw 'Existing evidence is preserved; use a new Name.'}
. (Join-Path $repo 'scripts/toolchain.ps1')
$tool=Enter-DesktopUpToolchain -ForPackaging -Offline
$results=[Collections.Generic.List[object]]::new()
function Assert-Check([bool]$Value,[string]$Message){if(!$Value){throw $Message}}
function Run([string]$File,[string]$Arguments=''){
    $process=Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(30000)){throw 'Fixture timeout; retain evidence.'}
    $process.Refresh()
    return $process.ExitCode
}
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    foreach($case in @('normal','already-missing','read-only','held-file','held-read-only','report-unwritable')){
        $dir=Join-Path $root $case
        New-Item -ItemType Directory -Path $dir | Out-Null
        $first=Join-Path $dir 'first.bin'
        $later=Join-Path $dir 'later.bin'
        $unknown=Join-Path $dir 'unknown-user-file.txt'
        $fixture=Join-Path $dir 'fixture.exe'
        $uninstall=Join-Path $dir 'Uninstall.exe'
        [IO.File]::WriteAllText($first,'owned first')
        [IO.File]::WriteAllText($later,'owned later')
        [IO.File]::WriteAllText($unknown,'do not delete')
        & $tool.MakeNsis /V2 /WX /INPUTCHARSET UTF8 "/DFIXTURE_ROOT=$dir" "/DFIXTURE_EXE=$fixture" /DRELEASE_ID=fixture "/DEVIDENCE_INCLUDE=$repo\packaging\preview\uninstall-evidence.nsh" (Join-Path $PSScriptRoot 'Fixtures/uninstall-evidence.nsi') 2>&1 |
            Tee-Object -FilePath (Join-Path $dir 'compile.log') | Out-Host
        if($LASTEXITCODE -ne 0){throw 'Evidence fixture compile failed.'}
        Assert-Check ((Run $fixture '/S') -eq 0) 'Fixture creation failed.'
        if($case -eq 'already-missing'){Remove-Item -LiteralPath $first}
        if($case -in @('read-only','held-read-only')){[IO.File]::SetAttributes($first,[IO.FileAttributes]::ReadOnly)}
        $lease=$null
        $shouldFail=$case -in @('held-file','held-read-only','report-unwritable')
        if($case -eq 'report-unwritable'){
            New-Item -ItemType Directory -Path (Join-Path $dir 'profile') | Out-Null
            [IO.File]::WriteAllText((Join-Path $dir 'profile/uninstall-reports'),'owned diagnostic obstruction')
        }
        try {
            if($shouldFail){$lease=[IO.File]::Open($first,'Open','Read','Read')}
            $code=Run $uninstall ('/S _?='+$dir)
            if($shouldFail){
                Assert-Check ($code -eq 24) "Failure must stop with 24: $case actual $code"
                Assert-Check ((Test-Path $first) -and (Test-Path $later)) 'Cleanup continued after failure.'
                $failure=Get-Content (Join-Path $dir 'result.ini') -Raw
                Assert-Check ($failure -match 'Api=DeleteFileW' -and $failure -match 'Win32Error=32') 'Original sharing violation was not captured.'
                if($case -eq 'held-read-only'){
                    Assert-Check (([IO.File]::GetAttributes($first) -band [IO.FileAttributes]::ReadOnly) -ne 0) 'Failed deletion changed read-only attribute.'
                }
                if($case -ne 'report-unwritable'){
                    $reports=@(Get-ChildItem (Join-Path $dir 'profile/uninstall-reports') -Filter 'cleanup-*.ini')
                    Assert-Check ($reports.Count -eq 1) 'Expected one immutable failure report.'
                    $report=Get-Content $reports[0].FullName -Raw
                    Assert-Check ($report -match 'Win32Error=32' -and $report -match 'UtcFileTime=\d+' -and $report -match 'AfterFailure=1') 'Incomplete report.'
                    Assert-Check ($report -match ('PidAndCreationFileTime=.*\b'+$PID+'@\d+;')) 'Known fixture owner missing from the bounded post-failure snapshot.'
                    $reportHash=(Get-FileHash $reports[0].FullName).Hash
                } else {
                    Assert-Check ($failure -match '(?m)^Report=\s*$') 'Unwritable report must not claim a saved path.'
                }
            } else {
                Assert-Check ($code -eq 0 -and !(Test-Path $first) -and !(Test-Path $later)) "Normal literal cleanup failed: $case"
            }
        } finally {if($lease){$lease.Dispose()}}
        if($shouldFail){
            Assert-Check ((Run $uninstall ('/S _?='+$dir)) -eq 0) 'Explicit retry failed.'
            Assert-Check (!(Test-Path $first) -and !(Test-Path $later)) 'Retry left payload.'
            if($case -ne 'report-unwritable'){Assert-Check ((Get-FileHash $reports[0].FullName).Hash -eq $reportHash) 'Retry overwrote initial evidence.'}
        }
        Assert-Check ([IO.File]::ReadAllText($unknown) -eq 'do not delete') 'Unknown file changed.'
        $results.Add(@{Case=$case;Passed=$true;FirstExitCode=$code;RootCauseOfHistoricalFailureEstablished=$false})
    }
    $results
} finally {
    if(Test-Path $root){$results | ConvertTo-Json | Set-Content (Join-Path $root 'results.json') -Encoding UTF8}
    Exit-DesktopUpToolchain $tool
}
