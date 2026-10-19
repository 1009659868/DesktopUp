#requires -Version 5.1
# Both scenarios are owned, isolated data. Configuration has no real-file references.
param(
    [ValidateSet('Configuration','Icons')][string]$Scenario='Configuration',
    [ValidateSet('Open','Prepare','Status')][string]$Action='Open'
)
$ErrorActionPreference='Stop'
$r1Repo=Split-Path -Parent $PSScriptRoot
$r1Candidate=Join-Path $r1Repo 'out/architecture-r1-configuration/candidate01'
$r1Product=Join-Path $r1Candidate 'app'
$r1Review=Join-Path $r1Repo ('out/drop-d3/architecture-r1-configuration-review01/'+$Scenario.ToLowerInvariant())
$r1Profile=Join-Path $r1Review 'profile'
$r1Fixture=Join-Path $r1Candidate 'fixture/DesktopUp.SessionFixture.exe'
$r1App=Join-Path $r1Product 'DesktopUp.exe'
$r1Ready=Join-Path $r1Review '.r1-configuration-ready'
if($Action -eq 'Status'){
    [pscustomobject]@{Scenario=$Scenario;Executable=$r1App;Profile=$r1Profile;Samples=$r1Review;Prepared=(Test-Path -LiteralPath $r1Ready)}
    return
}
if(Get-Process DesktopUp -ErrorAction SilentlyContinue){throw 'Exit DesktopUp normally first. Nothing stopped.'}
foreach($r1Path in @($r1App,$r1Fixture,(Join-Path $r1Product 'content-worker/DesktopUp.ContentWorker.exe'))){
    if(!(Test-Path -LiteralPath $r1Path -PathType Leaf)){throw "Missing R1 build: $r1Path"}
}
$r1Hashes=@{
    'DesktopUp.dll'='71B39BB79635D65898B0208580C7B07D81C83D5BE666230CD801E743C60F0487'
    'DesktopUp.Application.dll'='5F133B4D3E11F4EE3D13C72BBF517C17277FA6445AAC571355462D775EE75BDD'
    'DesktopUp.Infrastructure.dll'='32418D78AAC3DD6B28585E49F835FB0ED6072C7F5A93338B9CC78866F2C514B7'
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
        if($Scenario -eq 'Configuration'){
            & $r1Fixture $r1Profile --content-layout empty | Out-Null
            if($LASTEXITCODE -ne 0){throw 'Configuration fixture preparation failed; no overwrite.'}
        }else{
            [IO.File]::WriteAllText((Join-Path $r1Review '.uninstall-fixture'),'owned')
            & $r1Fixture $r1Profile --architecture-icons | Out-Null
            if($LASTEXITCODE -ne 0){throw 'Icon fixture preparation failed; no overwrite.'}
            Add-Type -AssemblyName System.Drawing
            $r1Image=New-Object Drawing.Bitmap(128,128)
            $r1Graphics=[Drawing.Graphics]::FromImage($r1Image)
            $r1Brush=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255,40,135,175))
            try {
                $r1Graphics.Clear([Drawing.Color]::Transparent)
                $r1Graphics.FillEllipse($r1Brush,4,4,120,120)
                $r1Image.Save((Join-Path $r1Review 'sample-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
            } finally {$r1Brush.Dispose();$r1Graphics.Dispose();$r1Image.Dispose()}
        }
        New-Item -ItemType Directory -Path (Join-Path $r1Review 'exports') | Out-Null
        [IO.File]::WriteAllText($r1Ready,'owned')
    }
    if(!(Test-Path -LiteralPath $r1Ready) -or [IO.File]::ReadAllText($r1Ready) -ne 'owned'){
        throw 'Not a completed owned R1 fixture. Nothing overwritten.'
    }
    if($Action -eq 'Prepare'){
        [pscustomobject]@{Scenario=$Scenario;Profile=$r1Profile;Samples=$r1Review;Executable=$r1App}; return
    }
    $r1Process=Start-DesktopUpFixtureProcess -ExecutablePath $r1App -DataRoot $r1Profile -Arguments '--show-control-center' -WindowStyle Hidden
    [pscustomobject]@{PID=$r1Process.Id;Scenario=$Scenario;Profile=$r1Profile;Samples=$r1Review;InstallerExecuted=$false}
} finally {Exit-DesktopUpToolchain $r1Tools}
