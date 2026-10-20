#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateRange(1, 2147483647)][int]$ProcessId,
    [Parameter(Mandatory = $true)][ValidateLength(1, 20)]
    [ValidatePattern('^(?:0[xX][0-9a-fA-F]{1,16}|[0-9]{1,20})$')][string]$InputWindowHandle,
    [Parameter(Mandatory = $true)][ValidateLength(1, 32767)][string]$ExpectedExecutablePath,
    [Parameter(Mandatory = $true)][ValidateSet('move', 'click', 'double-click', 'right-click', 'drag', 'cancel-drag')][string]$Action,
    [Parameter(Mandatory = $true)][int]$StartX,
    [Parameter(Mandatory = $true)][int]$StartY,
    [Nullable[int]]$EndX,
    [Nullable[int]]$EndY
)
$ErrorActionPreference = 'Stop'
if (-not [IO.Path]::IsPathRooted($ExpectedExecutablePath)) { throw 'ExpectedExecutablePath must be an absolute executable path.' }
$taskExecutable = [IO.Path]::GetFullPath($ExpectedExecutablePath)
if (-not (Test-Path -LiteralPath $taskExecutable -PathType Leaf) -or [IO.Path]::GetExtension($taskExecutable) -ine '.exe') {
    throw 'ExpectedExecutablePath must identify an existing executable file.'
}
if ($InputWindowHandle.StartsWith('0x', [StringComparison]::OrdinalIgnoreCase)) {
    $taskHandleValue = [Convert]::ToUInt64($InputWindowHandle.Substring(2), 16)
} else {
    $taskHandleValue = [UInt64]::Parse($InputWindowHandle, [Globalization.CultureInfo]::InvariantCulture)
}
if ($taskHandleValue -eq 0 -or $taskHandleValue -gt [Int64]::MaxValue -or
    ([IntPtr]::Size -eq 4 -and $taskHandleValue -gt [Int32]::MaxValue)) { throw 'InputWindowHandle is outside the supported nonzero native handle range.' }
if (($null -eq $EndX) -ne ($null -eq $EndY)) { throw 'EndX and EndY must be supplied together.' }
$taskDrag = $Action -in @('drag', 'cancel-drag')
if ($taskDrag -and ($null -eq $EndX -or $null -eq $EndY)) { throw 'Drag actions require EndX and EndY.' }
if (-not $taskDrag -and ($null -ne $EndX -or $null -ne $EndY)) { throw 'Click actions use only StartX and StartY.' }
$taskEndX = if ($taskDrag) { [int]$EndX } else { $StartX }
$taskEndY = if ($taskDrag) { [int]$EndY } else { $StartY }
if (-not ('DesktopUpContentNativeDriver' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'DesktopUpContentNativeDriver.cs')
}
# No process launch, activation, window movement, foreground takeover or application handler call.
# Right-click uses only StartX/StartY and reports separate RightDowns/RightUps/CompletedRightClicks.
# The outer isolated probe saves this fresh result together with its package and process evidence.
[DesktopUpContentNativeDriver]::Run($ProcessId, [IntPtr]::new([Int64]$taskHandleValue), $taskExecutable,
    $Action, $StartX, $StartY, $taskEndX, $taskEndY)
