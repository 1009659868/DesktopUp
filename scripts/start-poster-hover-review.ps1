#requires -Version 5.1
param()
$ErrorActionPreference = 'Stop'
$hoverRepo = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'start-poster-render-review.ps1') `
    -CandidateDirectory (Join-Path $hoverRepo 'out/poster-render/hover02') `
    -ReviewDirectory (Join-Path $hoverRepo 'out/drop-d3/poster-hover-review01')
