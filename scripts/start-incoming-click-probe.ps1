#requires -Version 5.1
$ErrorActionPreference='Stop'
$clickRepo=Split-Path -Parent $PSScriptRoot
$clickExe=Join-Path $clickRepo 'out/drop-d3/click-probe02/DesktopUp.NativeDragProbe.exe'
if(!(Test-Path -LiteralPath $clickExe)){throw 'Build the isolated click-probe02 candidate first; this script does not launch DesktopUp.'}
$clickRoot=Join-Path $clickRepo ('out/drop-d3/click-review-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $clickRoot | Out-Null
New-Item -ItemType File -Path (Join-Path $clickRoot 'd1-probe-owned.marker') | Out-Null
$clickCheck=Start-Process -FilePath $clickExe -ArgumentList @('--check-environment',('"'+$clickRoot+'"')) -WindowStyle Hidden -PassThru
if(!$clickCheck.WaitForExit(15000)){throw "Environment checker is still running PID $($clickCheck.Id); no forced termination."}
$clickEnvironment=Get-Content -LiteralPath (Join-Path $clickRoot 'environment.json') -Raw | ConvertFrom-Json
if($clickCheck.ExitCode -ne 0 -or !$clickEnvironment.Allowed){throw "Environment mismatch: $($clickEnvironment.Reason). No UAC/UIPI changes are allowed."}
# Interactive diagnostic explicitly intended for the user's click / drag check.
$clickProcess=Start-Process -FilePath $clickExe -ArgumentList @('--cross-click',('"'+$clickRoot+'"')) -WindowStyle Normal -PassThru
[pscustomobject]@{ProcessId=$clickProcess.Id;Review=$clickRoot;Environment=$clickEnvironment.Kind;Scope='Cross-process click + OLE probe only; no file moves or product configuration changes.'}
