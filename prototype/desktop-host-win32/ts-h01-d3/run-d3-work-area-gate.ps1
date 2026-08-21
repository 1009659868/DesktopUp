param(
  [ValidateSet(96, 120, 144, 192)]
  [int]$ExpectedDpi = 96,

  [ValidateRange(120000, 900000)]
  [int]$DurationMs = 600000
)

$ErrorActionPreference = 'Stop'
$probeRoot = $PSScriptRoot
$exe = Join-Path $probeRoot 'out\build\x64-release\Release\desktop-host-boundary-probe.exe'
if (-not (Test-Path -LiteralPath $exe)) {
  throw "D3 Release probe not found: $exe"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $probeRoot "out\d3-3-work-area-$stamp"
New-Item -ItemType Directory -Path $runRoot | Out-Null
$log = Join-Path $runRoot 'work-area-change.jsonl'

Write-Host 'TS-H01-D3-3：工作区与任务栏边界矩阵' -ForegroundColor Cyan
Write-Host "固定缩放 DPI：$ExpectedDpi；期望工作区变化：至少 10 次。"
Write-Host '请在 Windows 任务栏设置中完成 5 个循环：自动隐藏 开启 -> 关闭。' -ForegroundColor Yellow
Write-Host '每次切换后等待诊断卡稳定。不要拖动、缩放、收起或点击诊断卡。'

$arguments = @(
  '--run-boundary-test',
  '--scenario', 'display-change',
  '--expected-dpi', $ExpectedDpi.ToString(),
  '--duration-ms', $DurationMs.ToString(),
  '--expected-drags', '0',
  '--expected-resizes', '0',
  '--expected-collapse-cycles', '0',
  '--expected-display-changes', '10',
  '--log', $log
)
$process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait
$hash = if (Test-Path -LiteralPath $log) {
  (Get-FileHash -Algorithm SHA256 -LiteralPath $log).Hash
} else {
  $null
}
$finalResult = $null
if (Test-Path -LiteralPath $log) {
  try {
    $finalResult = (Get-Content -LiteralPath $log -Tail 1 | ConvertFrom-Json).result
  } catch {
    $finalResult = 'invalid_final_json'
  }
}
$passed = $process.ExitCode -eq 0 -and
          $finalResult -eq 'boundary_telemetry_pass_manual_visual_pending'
$summary = [ordered]@{
  schema = 'desktopup.ts-h01-d3.work-area-gate-summary.v1'
  recorded_at = (Get-Date).ToString('o')
  baseline_expected_dpi = $ExpectedDpi
  expected_taskbar_auto_hide_cycles = 5
  expected_work_area_changes_minimum = 10
  prohibited_card_interaction = $true
  exit_code = $process.ExitCode
  telemetry_result = $finalResult
  telemetry_pass = $passed
  manual_visual_result = 'PENDING_USER_CONFIRMATION'
  log = $log
  log_sha256 = $hash
}
$summaryPath = Join-Path $runRoot 'summary.json'
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host "Summary: $summaryPath"
if ($passed) {
  Write-Host 'D3-3 telemetry PASS；等待人工视觉确认。' -ForegroundColor Green
  exit 0
}
Write-Host 'D3-3 telemetry FAIL；保留日志。' -ForegroundColor Red
exit 4
