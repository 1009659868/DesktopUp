#requires -Version 5.1
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'scripts/toolchain.ps1')
$gateTools = Enter-DesktopUpToolchain -Offline
$root = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repo ('out/text-policy-' + [guid]::NewGuid().ToString('N')) }
if (!$root.StartsWith((Join-Path $repo 'out') + '\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $root)) { throw 'Choose a new directory under repository/out.' }
$utf8 = New-Object Text.UTF8Encoding($false, $true)
$utf8Bom = New-Object Text.UTF8Encoding($true, $true)
try {
    New-Item -ItemType Directory -Path (Join-Path $root 'scripts'),(Join-Path $root 'src/Sample'),(Join-Path $root 'src/DesktopUp.Infrastructure/Persistence/Migrations') | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'scripts/validate-source-text.ps1') -Destination (Join-Path $root 'scripts')
    Copy-Item -LiteralPath (Join-Path $repo 'Directory.Build.targets') -Destination $root
    $migrations = Join-Path $root 'src/DesktopUp.Infrastructure/Persistence/Migrations'
    Get-ChildItem -LiteralPath (Join-Path $repo 'src/DesktopUp.Infrastructure/Persistence/Migrations') -File |
        Where-Object { $_.Extension -in @('.sql','.xml') } | Copy-Item -Destination $migrations
    $source = Join-Path $root 'src/Sample/sample.cs'
    $script = Join-Path $root 'scripts/sample.ps1'
    [IO.File]::WriteAllText($source,"class Sample {}\n".Replace('\n',"`n"),$utf8)
    [IO.File]::WriteAllText($script,"# " + [char]0x4E2D + [char]0x6587 + "`r`n",$utf8Bom)
    $project = Join-Path $root 'gate.proj'
    [IO.File]::WriteAllText($project,'<Project><Import Project="Directory.Build.targets" /><Target Name="PrepareForBuild" /><Target Name="Publish" /></Project>',$utf8)
    $gate = Join-Path $root 'scripts/validate-source-text.ps1'
    & powershell.exe -NoProfile -NonInteractive -File $gate -RepositoryRoot $root
    if ($LASTEXITCODE) { throw 'Valid fixture rejected.' }

    $cases = @(
        @{Name='invalid-utf8';Path=$source;Bytes=[byte[]](0xFF)},
        @{Name='utf16';Path=$source;Bytes=[byte[]]([Text.Encoding]::Unicode.GetPreamble()+[Text.Encoding]::Unicode.GetBytes('class X {}'))},
        @{Name='utf16-no-bom';Path=$source;Bytes=[Text.Encoding]::Unicode.GetBytes('class X {}')},
        @{Name='source-bom';Path=$source;Bytes=[byte[]]($utf8Bom.GetPreamble()+$utf8Bom.GetBytes('class X {}'))},
        @{Name='source-crlf';Path=$source;Bytes=$utf8.GetBytes("class X {}`r`n")},
        @{Name='script-missing-bom';Path=$script;Bytes=$utf8.GetBytes("# " + [char]0x4E2D + "`r`n")},
        @{Name='script-lf';Path=$script;Bytes=[byte[]]($utf8Bom.GetPreamble()+$utf8Bom.GetBytes("# sample`n"))},
        @{Name='sql-change';Path=(Join-Path $migrations '001.sql');Bytes=$utf8.GetBytes('SELECT 2;')},
        @{Name='sql-crlf';Path=(Join-Path $migrations '001.sql');Bytes=$utf8.GetBytes([IO.File]::ReadAllText((Join-Path $migrations '001.sql')).Replace("`n","`r`n"))}
    )
    foreach ($case in $cases) {
        $original = [IO.File]::ReadAllBytes($case.Path)
        try {
            [IO.File]::WriteAllBytes($case.Path,$case.Bytes)
            $output = & powershell.exe -NoProfile -NonInteractive -File $gate -RepositoryRoot $root 2>&1
            if ($LASTEXITCODE -eq 0) { throw ('Invalid fixture accepted: ' + $case.Name) }
            [IO.File]::WriteAllText((Join-Path $root ($case.Name+'.log')),($output -join "`n"),$utf8)
            if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($case.Path)) -cne [Convert]::ToBase64String($case.Bytes)) { throw 'Validator rewrote fixture bytes.' }
            Write-Output ('Rejected without rewriting: ' + $case.Name)
        } finally { [IO.File]::WriteAllBytes($case.Path,$original) }
    }
    foreach ($target in @('PrepareForBuild','Publish')) {
        $original = [IO.File]::ReadAllBytes($source)
        try {
            [IO.File]::WriteAllBytes($source,[byte[]](0xFF))
            $output = & $gateTools.DotNet msbuild $project "-t:$target" -nologo 2>&1
            if ($LASTEXITCODE -eq 0 -or ($output -join ' ') -notmatch 'invalid UTF-8') { throw ('Common build gate did not reject: ' + $target) }
            [IO.File]::WriteAllText((Join-Path $root ($target+'.log')),($output -join "`n"),$utf8)
            Write-Output ('Common MSBuild gate rejected: ' + $target)
        } finally { [IO.File]::WriteAllBytes($source,$original) }
    }
    & $gateTools.DotNet msbuild $project '-t:PrepareForBuild;Publish' -nologo
    if ($LASTEXITCODE) { throw 'Common build gate rejected corrected fixture.' }
    Write-Output ('PASS: encoding/EOL negative cases, check-only behavior, direct build and no-build publish entry. Evidence: ' + $root)
} finally { Exit-DesktopUpToolchain $gateTools }
