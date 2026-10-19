#requires -Version 5.1
# Status only: no installer, application, registry write or fixture creation.
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$entry=Join-Path $repo 'scripts/start-uninstall-content-review.ps1'
function Test-ReviewStateWithoutRegistryDrive {
    $expected=& $entry -Action Status
    if($expected.State -notin @('Registered','Mismatch','UnregisteredFiles','Absent')){throw 'Unexpected installation state.'}
    if($expected.RegistryView -ne 'Registry64'){throw 'Installer registry view not explicit.'}
    # A function-local PSDrive shadows the inherited drive, without touching the registry.
    # This reproduces an unreadable/misresolved HKCU provider path, not a missing install.
    New-PSDrive -Name HKCU -PSProvider FileSystem -Root $PSScriptRoot -Scope Local | Out-Null
    if((Get-PSDrive HKCU).Provider.Name -ne 'FileSystem'){throw 'Registry-drive isolation did not take effect.'}
    $actual=& $entry -Action Status
    foreach($field in @('State','User','Registered','RegisteredDirectory','ExpectedDirectory','MarkerMatches','LauncherExists','AppExists','UninstallerExists','Profile')){
        if($expected.$field -cne $actual.$field){throw "Status depends on the HKCU PSDrive: $field"}
    }
    [pscustomobject]@{PowerShell=$PSVersionTable.PSVersion.ToString();State=$actual.State;Check='Status independent of HKCU PSDrive';Passed=$true}
}
Test-ReviewStateWithoutRegistryDrive
