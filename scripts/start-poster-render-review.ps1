#requires -Version 5.1
param([string]$CandidateDirectory, [string]$ReviewDirectory)
$ErrorActionPreference = 'Stop'
$renderRepo = Split-Path -Parent $PSScriptRoot
if (!$CandidateDirectory) { $CandidateDirectory = Join-Path $renderRepo 'out/poster-render/candidate05' }
$renderReview = if ($ReviewDirectory) { $ReviewDirectory } else { Join-Path $renderRepo 'out/drop-d3/poster-render-review01' }
# Reuse the existing isolated, owned four-item fixture and normal launch lifetime.
$renderProcess = & (Join-Path $PSScriptRoot 'start-card-icon-review.ps1') `
    -CandidateDirectory $CandidateDirectory `
    -ReviewDirectory $renderReview
$renderBatch = Join-Path $renderReview 'drag-in-samples'
if (!(Test-Path -LiteralPath $renderBatch)) {
    New-Item -ItemType Directory -Path $renderBatch | Out-Null
    for ($renderIndex = 1; $renderIndex -le 16; $renderIndex++) {
        $renderName = Join-Path $renderBatch ('render-sample-{0:00}.txt' -f $renderIndex)
        $renderWriter = New-Object IO.StreamWriter([IO.File]::Open($renderName, [IO.FileMode]::CreateNew))
        try { $renderWriter.Write('DesktopUp owned rendering review sample. No user data.') }
        finally { $renderWriter.Dispose() }
    }
}
$renderProcess
[pscustomobject]@{ DragInSamples = $renderBatch; Note = 'Only self-created files. Exit through tray before relaunch; existing profile is preserved.' }
