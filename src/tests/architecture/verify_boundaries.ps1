param(
  [Parameter(Mandatory = $true)]
  [string]$SourceRoot
)

$ErrorActionPreference = 'Stop'
$srcRoot = Join-Path $SourceRoot 'src'
$cmakePath = Join-Path $srcRoot 'CMakeLists.txt'
$cmake = Get-Content -LiteralPath $cmakePath -Raw

function Get-LinkBlock {
  param([string]$Target)
  $escaped = [regex]::Escape($Target)
  $match = [regex]::Match($cmake, "target_link_libraries\($escaped PRIVATE(?<body>[\s\S]*?)\)")
  if (-not $match.Success) {
    throw "QTS03_ARCH_TARGET_NOT_FOUND: $Target"
  }
  return $match.Groups['body'].Value
}

$runtimeLinks = Get-LinkBlock 'desktopup-runtime'
foreach ($forbidden in @('Qt6::QuickControls2', 'Qt6::WebView')) {
  if ($runtimeLinks -match [regex]::Escape($forbidden)) {
    throw "QTS03_ARCH_RUNTIME_FORBIDDEN_DEPENDENCY: $forbidden"
  }
}
if ($runtimeLinks -notmatch [regex]::Escape('desktopup_renderer_qtquick')) {
  throw 'QTS04_ARCH_RUNTIME_QTQUICK_RENDERER_MISSING'
}

$controlLinks = Get-LinkBlock 'desktopup-control-center'
foreach ($forbidden in @('desktopup_state_store_sqlite', 'desktopup_desktop_host_win32', 'winsqlite3')) {
  if ($controlLinks -match [regex]::Escape($forbidden)) {
    throw "QTS03_ARCH_CONTROL_CENTER_FORBIDDEN_DEPENDENCY: $forbidden"
  }
}

$frameworkFreeRoots = @(
  (Join-Path $srcRoot 'contracts'),
  (Join-Path $srcRoot 'core')
)
$frameworkFiles = foreach ($root in $frameworkFreeRoots) {
  Get-ChildItem -LiteralPath $root -Recurse -File -Include *.h,*.hpp,*.cpp
}
$forbiddenTokens = @(
  '#include <Q',
  '#include <windows.h>',
  '#include <winsqlite',
  'sqlite3',
  'HWND',
  'QQml',
  'QQuick'
)
foreach ($token in $forbiddenTokens) {
  $hit = $frameworkFiles | Select-String -SimpleMatch $token | Select-Object -First 1
  if ($hit) {
    throw "QTS03_ARCH_FRAMEWORK_FREE_VIOLATION: $token at $($hit.Path):$($hit.LineNumber)"
  }
}

$axiomSources = Get-ChildItem -LiteralPath $srcRoot -Recurse -File |
  Where-Object { $_.Extension -in @('.h', '.hpp', '.cpp', '.cmake') -or $_.Name -eq 'CMakeLists.txt' }
$axiomHit = $axiomSources |
  Select-String -Pattern 'axiom[\\/:_-]?runtime|axiom::runtime' -CaseSensitive:$false |
  Select-Object -First 1
if ($axiomHit) {
  throw "QTS03_ARCH_AXIOM_RUNTIME_DEPENDENCY: $($axiomHit.Path):$($axiomHit.LineNumber)"
}

Write-Output 'QTS03_ARCHITECTURE_PASS'
