#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$AppDirectory,
    [Parameter(Mandatory)][string]$DatabaseCopy,
    [Parameter(Mandatory)][string]$OutputRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$ExpectedAssemblySha256
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$taskApp=(Resolve-Path -LiteralPath $AppDirectory).Path.TrimEnd('\')
$taskDatabase=(Resolve-Path -LiteralPath $DatabaseCopy).Path
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
$taskNormal=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'DesktopUp\V3')).TrimEnd('\')
foreach($taskProtected in @($taskApp,$taskNormal)) {
    if($taskOutput.Equals($taskProtected,[StringComparison]::OrdinalIgnoreCase) -or
        $taskOutput.StartsWith($taskProtected+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Use an isolated output directory.'}
}
if(Test-Path -LiteralPath $taskOutput){throw 'Preserve existing evidence; choose a new output directory.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process was stopped.'}
$taskAssembly=Join-Path $taskApp 'DesktopUp.dll'
if((Get-FileHash $taskAssembly).Hash -ine $ExpectedAssemblySha256){throw 'Candidate hash differs.'}
function Get-BackdropBinding {
    @(Get-ChildItem -LiteralPath $taskApp -Recurse -File | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($taskApp.Length).TrimStart('\').Replace('\','/');
            Length=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
}
$taskBinding=Get-BackdropBinding
$taskSourceHash=(Get-FileHash -LiteralPath $taskDatabase).Hash
$taskData=Join-Path $taskOutput 'data'
New-Item -ItemType Directory -Path (Join-Path $taskData 'state') | Out-Null
Copy-Item -LiteralPath $taskDatabase -Destination (Join-Path $taskData 'state\desktopup.sqlite3')
$taskBinding | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $taskOutput 'artifacts.json') -Encoding UTF8
$taskProcess=Start-DesktopUpFixtureProcess -ExecutablePath (Join-Path $taskApp 'DesktopUp.exe') -DataRoot $taskData -Arguments '--backdrop-probe --exit-after-ms=60000' -WindowStyle Hidden
$taskDeadline=[DateTime]::UtcNow.AddSeconds(65)
while(!$taskProcess.WaitForExit(500)) {
    if([DateTime]::UtcNow -gt $taskDeadline){throw 'Probe timed out; process and evidence preserved.'}
}
$taskReport=Get-Content -LiteralPath (Join-Path $taskData 'backdrop-observation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskEvents=@(Get-Content -LiteralPath (Join-Path $taskData 'logs\desktopup.jsonl') -Encoding UTF8 | ForEach-Object {$_ | ConvertFrom-Json} | Where-Object processId -eq $taskProcess.Id)
$taskErrors=@($taskEvents | Where-Object level -in @('error','critical','fatal'))
$taskStopped=@($taskEvents | Where-Object eventName -eq 'application.stopped')
Add-Type -AssemblyName System.Drawing
# Side gutters contain no cards, labels or light beam. Compare the same native-composition
# pixels against the explicitly erased final reference, not XAML or palette declarations.
$taskReference=[Drawing.Bitmap]::new((Join-Path $taskData 'backdrop-erase-redraw.png'))
$taskComparisons=@()
try {
    foreach($taskName in @('initial','expanded-1','expanded-2','ordinary-redraw')) {
        $taskBitmap=[Drawing.Bitmap]::new((Join-Path $taskData ('backdrop-'+$taskName+'.png')))
        try {
            if($taskName -eq 'initial' -and $taskBitmap.Height -lt 100){continue} # Cold start collapsed.
            if($taskBitmap.Size -ne $taskReference.Size){throw 'Native capture dimensions differ.'}
            foreach($taskX in @(10,($taskReference.Width-10))) {
                foreach($taskY in @(85,[int]($taskReference.Height/2),($taskReference.Height-20))) {
                    $taskActual=$taskBitmap.GetPixel($taskX,$taskY); $taskExpected=$taskReference.GetPixel($taskX,$taskY)
                    $taskDelta=[Math]::Max([Math]::Abs([int]$taskActual.R-$taskExpected.R),[Math]::Max([Math]::Abs([int]$taskActual.G-$taskExpected.G),[Math]::Abs([int]$taskActual.B-$taskExpected.B)))
                    $taskComparisons += [pscustomobject]@{Name=$taskName;X=$taskX;Y=$taskY;Actual=$taskActual.ToArgb();Expected=$taskExpected.ToArgb();Delta=$taskDelta;Passed=($taskDelta -le 2)}
                }
            }
        } finally {$taskBitmap.Dispose()}
    }
} finally {$taskReference.Dispose()}
$taskStable=($taskBinding | ConvertTo-Json -Compress) -ceq ((Get-BackdropBinding) | ConvertTo-Json -Compress)
$taskSourceStable=(Get-FileHash -LiteralPath $taskDatabase).Hash -eq $taskSourceHash
$taskPassed=$taskReport.passed -and $taskProcess.ExitCode -eq 0 -and $taskStable -and $taskSourceStable -and
    $taskReport.rows.Count -eq 7 -and $taskStopped.Count -eq 1 -and $taskStopped[0].data.activeResources -eq 0 -and
    $taskErrors.Count -eq 0 -and $taskComparisons.Count -ge 18 -and @($taskComparisons | Where-Object {!$_.Passed}).Count -eq 0
$taskResult=[pscustomobject]@{Passed=$taskPassed;ProcessId=$taskProcess.Id;ExitCode=$taskProcess.ExitCode;
    ExpectedAssemblySha256=$ExpectedAssemblySha256;ArtifactsStable=$taskStable;SourceDatabaseUnchanged=$taskSourceStable;
    SourceDatabaseSha256=$taskSourceHash;PixelComparisons=$taskComparisons;Errors=$taskErrors;
    Cleanup=@($taskStopped.data);Scope='Own HWND native composition; stable collapse/expand endpoints';
    NativeMouse=$false;AnimationFramesCertified=$false;MilestoneAccepted=$false}
$taskResult | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $taskOutput 'observation.json') -Encoding UTF8
if(!$taskPassed){throw 'Native backdrop regression failed; evidence preserved.'}
$taskResult | Select-Object Passed,ProcessId,ArtifactsStable,SourceDatabaseUnchanged,Scope
