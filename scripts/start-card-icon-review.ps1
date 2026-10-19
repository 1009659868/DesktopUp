#requires -Version 5.1
param([string]$CandidateDirectory, [string]$ReviewDirectory)
$ErrorActionPreference='Stop'
$iconRepo=Split-Path -Parent $PSScriptRoot
if(!$CandidateDirectory){$CandidateDirectory=Join-Path $iconRepo 'out/card-icons-v2/candidate03'}
if(!$ReviewDirectory){$ReviewDirectory=Join-Path $iconRepo 'out/drop-d3/card-icons-v2-review01'}
$iconReview=[IO.Path]::GetFullPath($ReviewDirectory).TrimEnd('\')
$iconAllowed=[IO.Path]::GetFullPath((Join-Path $iconRepo 'out/drop-d3')).TrimEnd('\')+'\'
if(!$iconReview.StartsWith($iconAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Review must stay inside repository/out/drop-d3.'}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first; no process stopped.'}
$iconApp=Join-Path $CandidateDirectory 'app/DesktopUp.exe'
$iconFixture=Join-Path $CandidateDirectory 'fixture/DesktopUp.SessionFixture.exe'
if(!(Test-Path -LiteralPath $iconApp) -or !(Test-Path -LiteralPath $iconFixture)){throw 'Build the feature candidate and fixture first; see CONTENT_ICON_ASSETS.md.'}
. (Join-Path $PSScriptRoot 'toolchain.ps1')
. (Join-Path $PSScriptRoot 'Start-DesktopUpFixtureProcess.ps1')
$iconTools=Enter-DesktopUpToolchain
try {
    $iconProfile=Join-Path $iconReview 'profile'
    $iconMarker=Join-Path $iconReview '.card-icon-review-ready'
    if(!(Test-Path -LiteralPath $iconReview)){
        New-Item -ItemType Directory -Path $iconReview | Out-Null
        [IO.File]::WriteAllText((Join-Path $iconReview '.incoming-fixture'),'owned')
        & $iconFixture $iconProfile --content-layout empty | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Review fixture creation failed.'}
        $seed=Start-DesktopUpFixtureProcess -ExecutablePath $iconApp -DataRoot $iconProfile -Arguments '--incoming-probe' -WindowStyle Hidden
        if(!$seed.WaitForExit(45000)){throw "Fixture not finished; PID $($seed.Id) preserved."}
        $events=@(Get-Content -LiteralPath (Join-Path $iconProfile 'logs/desktopup.jsonl') | ForEach-Object {$_|ConvertFrom-Json})
        if($seed.ExitCode -ne 0 -or !($events | Where-Object eventName -eq 'contract.incoming_passed')){throw 'Fixture preparation failed; choose a new review directory.'}
        # Synthetic test image only, not a user asset. No source files or desktop icons used.
        Add-Type -AssemblyName System.Drawing
        $bitmap=New-Object System.Drawing.Bitmap(256,256)
        $graphics=[Drawing.Graphics]::FromImage($bitmap)
        $brush=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255,40,135,175))
        $pen=New-Object Drawing.Pen([Drawing.Color]::White,18)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.FillEllipse($brush,12,12,232,232)
            $graphics.DrawLine($pen,65,129,111,173)
            $graphics.DrawLine($pen,111,173,193,79)
            $bitmap.Save((Join-Path $iconReview 'sample-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
        } finally {$pen.Dispose();$brush.Dispose();$graphics.Dispose();$bitmap.Dispose()}
        [IO.File]::WriteAllText($iconMarker,'owned')
    }
    if(!(Test-Path -LiteralPath $iconMarker) -or [IO.File]::ReadAllText($iconMarker) -ne 'owned'){throw 'Existing directory is not a completed icon review fixture; no data overwritten.'}
    $app=Start-DesktopUpFixtureProcess -ExecutablePath $iconApp -DataRoot $iconProfile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{ProcessId=$app.Id;Profile=$iconProfile;SampleIcon=(Join-Path $iconReview 'sample-icon.png');Executable=$iconApp}
} finally {Exit-DesktopUpToolchain $iconTools}
