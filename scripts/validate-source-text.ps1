#requires -Version 5.1
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$textRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
$extensions = @('.cs','.sql','.xml','.xaml','.csproj','.props','.targets','.json','.config','.nsi','.nsh','.sln','.ps1','.cmd')
$files = @(
    Get-ChildItem -LiteralPath $textRoot -File | Where-Object { $_.Extension.ToLowerInvariant() -in $extensions -or $_.Name -in @('.gitattributes','.editorconfig') }
    foreach ($area in @('src','Modules','config','packaging','scripts','tests','tools')) {
        $areaPath = Join-Path $textRoot $area
        if (Test-Path -LiteralPath $areaPath) {
            Get-ChildItem -LiteralPath $areaPath -Recurse -File |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules)[\\/]' -and $_.Extension.ToLowerInvariant() -in $extensions }
        }
    }
)
$issues = New-Object 'System.Collections.Generic.List[string]'
foreach ($file in $files) {
    $relative = $file.FullName.Substring($textRoot.Length).TrimStart([char]92,[char]47)
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    try { $content = $strictUtf8.GetString($bytes) }
    catch { $issues.Add($relative + ': invalid UTF-8 bytes (replacement decoding is forbidden).'); continue }
    if ($content.Contains([string][char]0)) { $issues.Add($relative + ': NUL bytes are not allowed in source text (possible BOM-less UTF-16/UTF-32).') }
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $isPowerShell = $file.Extension -ieq '.ps1'
    if ($isPowerShell) {
        if (!$bom) { $issues.Add($relative + ': PowerShell 5.1 scripts require a UTF-8 BOM.') }
    } elseif ($bom) { $issues.Add($relative + ': UTF-8 BOM is not allowed.') }
    if ($isPowerShell -or $file.Extension -ieq '.cmd') {
        if ($content -match '(?<!\r)\n|\r(?!\n)') { $issues.Add($relative + ': expected CRLF, not LF/mixed newlines.') }
    } elseif ($content.Contains("`r")) { $issues.Add($relative + ': expected LF, not CRLF/mixed newlines.') }
}
# Frozen migration integrity is mandatory for build AND publish, before any database access.
$migrationRoot = Join-Path $textRoot 'src/DesktopUp.Infrastructure/Persistence/Migrations'
$catalogPath = Join-Path $migrationRoot 'catalog.xml'
if (!(Test-Path -LiteralPath $catalogPath)) { $issues.Add('Missing frozen migration catalog.') }
else {
    [xml]$catalog = $strictUtf8.GetString([IO.File]::ReadAllBytes($catalogPath))
    $version = 0
    foreach ($entry in $catalog.migrations.migration) {
        $version++
        $expectedFile = '{0:000}.sql' -f $version
        if ([int]$entry.version -ne $version -or $entry.file -cne $expectedFile -or !$entry.name -or $entry.sha256 -cnotmatch '^[0-9A-F]{64}$') {
            $issues.Add('Invalid migration identity/order: ' + $entry.version); continue
        }
        $sqlPath = Join-Path $migrationRoot $expectedFile
        if (!(Test-Path -LiteralPath $sqlPath)) { $issues.Add('Missing frozen SQL: ' + $expectedFile); continue }
        $sqlBytes = [IO.File]::ReadAllBytes($sqlPath)
        if ($sqlBytes.Length -eq 0 -or $sqlBytes[$sqlBytes.Length - 1] -eq 10) { $issues.Add($expectedFile + ': frozen SQL must be nonempty with no terminal newline.') }
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($sqlBytes)).Replace('-','') }
        finally { $sha.Dispose() }
        if ($actualHash -cne $entry.sha256) { $issues.Add($expectedFile + ': frozen migration SHA256 mismatch. Add a new migration; do not edit history.') }
        foreach ($legacy in $entry.legacy) {
            if ($null -ne $legacy -and ($legacy.sha256 -cnotmatch '^[0-9A-F]{64}$' -or !$legacy.evidence)) {
                $issues.Add($expectedFile + ': legacy digest needs an explicit digest and evidence.')
            }
        }
    }
    if (!$version -or @(Get-ChildItem -LiteralPath $migrationRoot -Filter '*.sql' -File).Count -ne $version) { $issues.Add('Migration resource count does not match catalog.') }
}
if ($issues.Count) { throw ("Source text validation failed (check-only; no files changed):`n" + ($issues -join "`n")) }
Write-Output ("Source text policy passed: {0} files; {1} frozen migrations." -f $files.Count, $version)
