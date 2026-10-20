#requires -Version 5.1
param([ValidatePattern('^[a-z0-9-]+$')][string]$RunName='paired02')
$ErrorActionPreference='Stop'
$measureRepo=Split-Path -Parent $PSScriptRoot
if(Get-Process -Name DesktopUp,DesktopUp.SessionFixture -ErrorAction SilentlyContinue){
    throw 'Exit DesktopUp and other cost fixtures normally before measurement. Nothing stopped.'
}
$measureBinaries=@{
    before=Join-Path $measureRepo 'out/architecture-p1-cost/fixture03/DesktopUp.SessionFixture.exe'
    after=Join-Path $measureRepo 'out/architecture-p1-shared/candidate02/fixture/DesktopUp.SessionFixture.exe'
}
foreach($binary in $measureBinaries.Values){
    if(!(Test-Path -LiteralPath $binary)){throw "Missing fixture: $binary"}
}
$measureReport=Join-Path $measureRepo "out/architecture-p1-shared/comparison-$RunName.json"
if(Test-Path -LiteralPath $measureReport){throw 'Measurement report exists; choose a new RunName.'}
$measureResults=New-Object System.Collections.Generic.List[object]
$measureEnvironment=[ordered]@{
    OS=[Environment]::OSVersion.ToString()
    LogicalProcessors=[Environment]::ProcessorCount
    PowerScheme=(& powercfg.exe /getactivescheme | Out-String).Trim()
    Notes='Separate processes; tiered JIT off; plain timings exclude trace and proxies. Other system load is not controlled. Process CPU/peak memory include all fixture setup and probes, not one reconcile.'
}
function Save-Measurement {
    [IO.File]::WriteAllText($measureReport,(@{Environment=$measureEnvironment;Runs=$measureResults.ToArray()} |
        ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
$measureTools=Enter-DesktopUpToolchain -Offline
try {
    $ordinals=@{before=0;after=0}
    # Alternate the order of each pair, preserving ALL samples and the first failure.
    foreach($variant in @('before','after','after','before','before','after')){
        $ordinals[$variant]++
        $output=Join-Path $measureRepo ("out/drop-d3/architecture-p1-"+$RunName+"-"+$variant+$ordinals[$variant])
        if(Test-Path -LiteralPath $output){throw "Output exists: $output"}
        $directory=Split-Path -Parent $measureBinaries[$variant]
        $hashes=@{}
        foreach($name in @('DesktopUp.SessionFixture.dll','DesktopUp.Application.dll','DesktopUp.Infrastructure.dll')){
            $hashes[$name]=(Get-FileHash -LiteralPath (Join-Path $directory $name)).Hash
        }
        $entry=[ordered]@{Variant=$variant;Output=$output;Hashes=$hashes;Started=[DateTimeOffset]::UtcNow;Status='Running'}
        $measureResults.Add($entry);Save-Measurement
        $start=New-Object Diagnostics.ProcessStartInfo
        $start.FileName=$measureBinaries[$variant];$start.UseShellExecute=$false
        $start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
        $start.EnvironmentVariables['DOTNET_TieredCompilation']='0'
        $start.Arguments='"'+$output+'" --architecture-attribution'
        $process=[Diagnostics.Process]::Start($start)
        $entry.PID=$process.Id;Save-Measurement
        $process.WaitForExit()
        $entry.ExitCode=$process.ExitCode
        $entry.CpuSeconds=$process.TotalProcessorTime.TotalSeconds
        $entry.ElapsedSeconds=($process.ExitTime-$process.StartTime).TotalSeconds
        $entry.PeakWorkingSetBytes=$process.PeakWorkingSet64
        $entry.Status=if($process.ExitCode -eq 0){'Completed'}else{'Failed'}
        Save-Measurement
        if($process.ExitCode -ne 0){throw "Fixture failed. Evidence retained: $output. No retry."}
        Write-Host "$variant complete: $output"
        $process.Dispose()
    }
} finally {Exit-DesktopUpToolchain $measureTools}
