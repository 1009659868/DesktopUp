#requires -Version 5.1
# Read-only evaluated build checks. Does not build, launch, install, or touch user data.
param([switch]$Offline, [switch]$VerifyMoveBytes)
$ErrorActionPreference = 'Stop'
$archRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'toolchain.ps1')
$archTools = Enter-DesktopUpToolchain -Offline:$Offline
try {
    $archProject = Join-Path $archRoot 'src/DesktopUp.App/DesktopUp.App.csproj'
    $archReviewRoot = [IO.Path]::GetFullPath((Join-Path $archRoot 'tools/review'))
    $archRows = @()
    foreach ($enabled in @('false','true')) {
        $raw = & $archTools.DotNet msbuild $archProject -nologo -p:Platform=x64 "-p:DesktopUpReviewTools=$enabled" -getItem:Compile -getProperty:DesktopUpReviewTools
        if ($LASTEXITCODE -ne 0) { throw 'Build evaluation failed.' }
        $evaluated = ($raw -join [Environment]::NewLine) | ConvertFrom-Json
        if ($evaluated.Properties.DesktopUpReviewTools -cne $enabled) { throw 'Review property did not reach evaluation.' }
        $sources = @($evaluated.Items.Compile | ForEach-Object { [IO.Path]::GetFullPath($_.FullPath) })
        if (@($sources | Group-Object | Where-Object Count -gt 1).Count) { throw 'Duplicate compile source.' }
        if (@($sources | Where-Object { !(Test-Path -LiteralPath $_ -PathType Leaf) }).Count) { throw 'Missing compile source.' }
        $review = @($sources | Where-Object { $_.StartsWith($archReviewRoot+'\',[StringComparison]::OrdinalIgnoreCase) })
        if ($enabled -eq 'false') {
            if ($review.Count -ne 0) { throw 'Product build includes review executors.' }
            $product = $sources
        } else {
            $expected = @(Get-ChildItem -LiteralPath (Join-Path $archReviewRoot 'App') -Filter '*.cs' -File | ForEach-Object FullName)
            foreach($module in @('Lamp','Rain')) {
                if(Test-Path -LiteralPath (Join-Path $archRoot "Modules/$module/Domain/${module}Declaration.cs")) {
                    $expected += @(Get-ChildItem -LiteralPath (Join-Path $archReviewRoot "Modules/$module/App") -Filter '*.cs' -File | ForEach-Object FullName)
                }
            }
            if (!$expected.Count -or @(Compare-Object ($expected|Sort-Object) ($review|Sort-Object)).Count) { throw 'Review link set differs from explicit sources.' }
            $normal = @($sources | Where-Object { !$_.StartsWith($archReviewRoot+'\',[StringComparison]::OrdinalIgnoreCase) })
            if (@(Compare-Object ($product|Sort-Object) ($normal|Sort-Object)).Count) { throw 'Review build changes production source set.' }
        }
        $archRows += [pscustomobject]@{Check='CompileSet';ReviewTools=$enabled;Sources=$sources.Count;ReviewSources=$review.Count;Result='PASS'}
    }
    $guard = & $archTools.DotNet msbuild $archProject -nologo -t:ValidateReviewBuildBoundary -p:DesktopUpPreview=true -p:DesktopUpReviewTools=true 2>&1
    if ($LASTEXITCODE -eq 0 -or ($guard -join ' ') -notmatch 'Preview packages must not include development review executors') {
        throw 'Preview/review conflict did not fail for the intended reason.'
    }
    $archRows += [pscustomobject]@{Check='PreviewGuard';Result='PASS'}
    if ($VerifyMoveBytes) {
        $moves = (Get-Content -LiteralPath (Join-Path $archRoot 'Docs/A_Project/40_Implementation/Architecture/A_MOVES.json') -Raw | ConvertFrom-Json).moves
        foreach ($move in $moves) {
            $target = [IO.Path]::GetFullPath((Join-Path $archRoot $move.to))
            if (!$target.StartsWith($archRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or
                (Test-Path -LiteralPath (Join-Path $archRoot $move.from)) -or
                !(Test-Path -LiteralPath $target -PathType Leaf) -or
                (Get-FileHash -LiteralPath $target).Hash -ine $move.sha256) { throw "Move changed or incomplete: $($move.from)" }
        }
        $archRows += [pscustomobject]@{Check='PureMoves';Count=$moves.Count;Result='PASS'}
    }
    $archRows | ConvertTo-Json -Depth 4
} finally { Exit-DesktopUpToolchain $archTools }
