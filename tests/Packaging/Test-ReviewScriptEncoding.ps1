#requires -Version 5.1
# Parse only. Never executes the review scripts, installer or file-return operations.
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
foreach($name in @('start-uninstall-content-review.ps1','toolchain.ps1','Start-DesktopUpFixtureProcess.ps1')){
    $path=Join-Path $repo ('scripts/'+$name)
    $bytes=[IO.File]::ReadAllBytes($path)
    $bom=$bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    if(@($bytes | Where-Object {$_ -gt 127}).Count -gt 0 -and !$bom){throw "Non-ASCII script requires a UTF-8 BOM for PowerShell 5.1: $name"}
    $tokens=$null;$issues=$null
    $ast=[System.Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$issues)
    if(@($issues).Count){throw "$name has $(@($issues).Count) parser errors: $($issues[0].Message)"}
    if($name -eq 'start-uninstall-content-review.ps1'){
        $prefix=[string][char]0x8BF7+[char]0x5148
        $messages=@($ast.FindAll({param($node) $node -is [System.Management.Automation.Language.StringConstantExpressionAst] -and $node.Value.StartsWith($prefix)},$true))
        if($messages.Count -ne 2){throw 'Chinese guard messages were not decoded correctly.'}
    }
    [pscustomobject]@{PowerShell=$PSVersionTable.PSVersion.ToString();Script=$name;Errors=0;Passed=$true}
}
