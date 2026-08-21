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
$runRoot = Join-Path $probeRoot "out\d3-2-resolution-$stamp"
New-Item -ItemType Directory -Path $runRoot | Out-Null
$log = Join-Path $runRoot 'resolution-change.jsonl'

Write-Host 'TS-H01-D3-2：分辨率改变/恢复矩阵' -ForegroundColor Cyan
Write-Host "固定缩放 DPI：$ExpectedDpi；期望显示几何变化：至少 10 次。"
Write-Host '请使用 Windows 显示设置完成 5 个循环：原生分辨率 -> 一个受支持的较低分辨率 -> 原生分辨率。' -ForegroundColor Yellow
Write-Host '每次改变后保留设置并等待诊断卡稳定，再执行下一次。探针不会修改显示配置。'

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
  schema = 'desktopup.ts-h01-d3.resolution-gate-summary.v1'
  recorded_at = (Get-Date).ToString('o')
  baseline_expected_dpi = $ExpectedDpi
  expected_resolution_cycles = 5
  expected_display_changes_minimum = 10
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
  Write-Host 'D3-2 telemetry PASS；等待人工视觉确认。' -ForegroundColor Green
  exit 0
}
Write-Host 'D3-2 telemetry FAIL；保留日志。' -ForegroundColor Red
exit 4
