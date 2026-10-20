#requires -Version 5.1
$ErrorActionPreference='Stop'
if (-not ('DesktopUpM08InputProbe' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'M08InputProbe.cs') }
$taskProbe=[DesktopUpM08InputProbe]::Read()
[pscustomobject]@{ TimestampUtc=[DateTimeOffset]::UtcNow; InputDesktopReadable=$taskProbe[0] -eq 'True';
 OpenError=[int]$taskProbe[1]; InputName=$taskProbe[2]; ThreadName=$taskProbe[3]; CursorReadable=$taskProbe[4] -eq 'True';
 CursorError=[int]$taskProbe[5]; Foreground=$taskProbe[6];
 Readable=($taskProbe[0] -eq 'True' -and $taskProbe[4] -eq 'True' -and $taskProbe[2] -eq $taskProbe[3]);
 Scope='read-only; no input, desktop switch, or permission change' }
