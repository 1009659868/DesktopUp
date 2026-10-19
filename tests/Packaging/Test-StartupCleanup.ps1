#requires -Version 5.1
# Compile the exact uninstall registration block against a private, non-startup
# registry key. This never registers anything in Windows Run/StartupApproved.
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'scripts/toolchain.ps1')
$tools=Get-DesktopUpToolchain
$id=[Guid]::NewGuid().ToString('N')
$output=Join-Path $repo ('out/startup/cleanup-'+$id)
New-Item -ItemType Directory -Path $output | Out-Null
$keyName='Software\DesktopUp\StartupCleanupTest\'+$id
$base=[Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser','Registry64')
$created=$false
try {
    $existing=$base.OpenSubKey($keyName)
    if($existing){$existing.Dispose();throw 'Test key already exists.'}
    $key=$base.CreateSubKey($keyName);$key.Dispose();$created=$true
    $installer=Get-Content -LiteralPath (Join-Path $repo 'packaging/preview/installer.nsi') -Raw -Encoding UTF8
    $match=[regex]::Match($installer,'(?s)Section "Uninstall"\s+Call un\.PrepareContents(?<block>.*?)  SetOutPath "\$TEMP"')
    if(!$match.Success){throw 'Uninstall boundary changed; inspect test extraction.'}
    $block=$match.Groups['block'].Value.Replace('Software\Microsoft\Windows\CurrentVersion\Run',$keyName)
    if($block.Contains('CurrentVersion\Run')){throw 'Unexpected real startup reference.'}
    $exe=Join-Path $output 'cleanup.exe'
    $source=@'
Unicode true
!include "LogicLib.nsh"
Name "DesktopUp isolated startup cleanup check"
OutFile "__EXE__"
RequestExecutionLevel user
SilentInstall silent
Section
  SetRegView 64
  StrCpy $INSTDIR "C:\DesktopUp isolated startup"
__BLOCK__
SectionEnd
'@
    $source=$source.Replace('__EXE__',$exe).Replace('__BLOCK__',$block)
    $nsi=Join-Path $output 'cleanup.nsi'
    [IO.File]::WriteAllText($nsi,$source,(New-Object Text.UTF8Encoding($true)))
    & $tools.MakeNsis /V2 /WX /INPUTCHARSET UTF8 $nsi
    if($LASTEXITCODE -ne 0){throw 'Cleanup harness compile failed.'}
    $owned='"C:\DesktopUp isolated startup\DesktopUp.exe" --autostart'
    $foreign='"C:\Other DesktopUp\DesktopUp.exe" --autostart'
    $results=@()
    foreach($case in @('absent','owned','foreign')){
        $key=$base.OpenSubKey($keyName,$true)
        try {
            $key.DeleteValue('DesktopUp',$false)
            $key.SetValue('Unrelated','keep')
            if($case -eq 'owned'){$key.SetValue('DesktopUp',$owned)}
            if($case -eq 'foreign'){$key.SetValue('DesktopUp',$foreign)}
        } finally {$key.Dispose()}
        $process=Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
        if(!$process.WaitForExit(10000)){throw 'Cleanup harness did not exit.'}
        $process.Refresh()
        if($process.ExitCode -ne 0){throw ('Cleanup failed: '+$case)}
        $key=$base.OpenSubKey($keyName)
        try {$value=$key.GetValue('DesktopUp');$unrelated=$key.GetValue('Unrelated')} finally {$key.Dispose()}
        if($unrelated -ne 'keep' -or ($case -eq 'foreign' -and $value -ne $foreign) -or ($case -ne 'foreign' -and $null -ne $value)){
            throw ('Ownership check failed: '+$case)
        }
        $results += [pscustomobject]@{Case=$case;Result='PASS'}
    }
    $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding UTF8
    $results | Format-Table | Out-Host
} finally {
    if($created){$base.DeleteSubKey($keyName,$false)} # Exact test leaf only; no Run key is opened.
    $base.Dispose()
}
