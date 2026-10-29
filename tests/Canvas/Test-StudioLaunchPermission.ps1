#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repoRoot 'scripts\Canvas\Get-StudioLaunchPermission.ps1')

function Snapshot([int]$level, [string]$user = 'review-user', [int]$session = 1) {
    return [pscustomobject]@{ UserSid=$user; SessionId=$session; IntegrityRid=$level }
}
$cases = @(
    @{Name='Medium matched'; App=(Snapshot 0x2000); Shell=(Snapshot 0x2000); Expected='Matched'},
    @{Name='High matched is allowed'; App=(Snapshot 0x3000); Shell=(Snapshot 0x3000); Expected='Matched'},
    @{Name='Higher application'; App=(Snapshot 0x3000); Shell=(Snapshot 0x2000); Expected='Mismatch'},
    @{Name='Higher shell'; App=(Snapshot 0x2000); Shell=(Snapshot 0x3000); Expected='Mismatch'},
    @{Name='Different user'; App=(Snapshot 0x2000); Shell=(Snapshot 0x2000 'other'); Expected='Mismatch'},
    @{Name='Different session'; App=(Snapshot 0x2000); Shell=(Snapshot 0x2000 'review-user' 2); Expected='Mismatch'},
    @{Name='Missing shell'; App=(Snapshot 0x2000); Shell=$null; Expected='Unknown'}
)
foreach ($case in $cases) {
    $result = Compare-StudioLaunchPermission -Application $case.App -Shell $case.Shell
    if ($result.Status -ne $case.Expected) { throw "Failed: $($case.Name)" }
}
$unavailable = Get-StudioLaunchPermission -ProcessId ([int]::MaxValue)
if ($unavailable.Status -ne 'Unknown') { throw 'An unavailable process must not be reported as a permission mismatch.' }
$actual = Get-StudioLaunchPermission -ProcessId $PID
if ($actual.Status -eq 'Unknown') { throw "Native token readback failed: $($actual.Reason)" }
[pscustomobject]@{CasesPassed=($cases.Count + 1); NativeReadback=$actual.Status; PowerShell=$PSVersionTable.PSVersion.ToString(); SystemSettingsChanged=$false}
