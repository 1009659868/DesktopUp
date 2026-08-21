param(
  [Parameter(Mandatory = $true)]
  [ValidateSet(96, 120, 144, 192)]
  [int]$ExpectedDpi,

  [ValidateRange(1, 5)]
  [int]$Runs = 5,

  [ValidateRange(30000, 300000)]
  [int]$DurationMs = 180000
)

$ErrorActionPreference = 'Stop'

$scaleLabel = switch ($ExpectedDpi) {
  96 { '100' }
  120 { '125' }
  144 { '150' }
  192 { '200' }
}

$probeRoot = $PSScriptRoot
$exe = Join-Path $probeRoot 'out\build\x64-release\Release\desktop-host-boundary-probe.exe'
if (-not (Test-Path -LiteralPath $exe)) {
  throw "D3 Release probe not found: $exe"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $probeRoot "out\d3-1-dpi-$scaleLabel-$stamp"
New-Item -ItemType Directory -Path $runRoot | Out-Null

$results = @()
Write-Host "TS-H01-D3-1: $scaleLabel% / expected DPI $ExpectedDpi" -ForegroundColor Cyan
Write-Host 'Windows 缩放必须由用户预先设置；本脚本不会修改系统显示设置。'
Write-Host '每次冷启动完成：标题拖动 1 次、绿色右下角缩放 1 次、琥珀色收起并展开 1 个完整循环。'

for ($index = 1; $index -le $Runs; $index++) {
  $log = Join-Path $runRoot ('run-{0:D2}.jsonl' -f $index)
  Write-Host ""
  Write-Host "[$index/$Runs] 已启动新的探针进程。请完成 1×拖动、1×缩放、1×收起/展开。" -ForegroundColor Yellow
  if ($index -eq $Runs) {
    Write-Host '本轮同时检查：普通程序覆盖卡片、桌面空白原生输入、无白条/灰框/裁切。'
  }

  $arguments = @(
    '--run-boundary-test',
    '--scenario', 'interactive',
    '--expected-dpi', $ExpectedDpi.ToString(),
    '--duration-ms', $DurationMs.ToString(),
    '--expected-drags', '1',
    '--expected-resizes', '1',
    '--expected-collapse-cycles', '1',
    '--expected-display-changes', '0',
    '--log', $log
  )
  $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait
  $hash = if (Test-Path -LiteralPath $log) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $log).Hash
  } else {
    $null
  }
  $finalLine = if (Test-Path -LiteralPath $log) {
    Get-Content -LiteralPath $log -Tail 1
  } else {
    $null
  }
  $finalResult = $null
  if ($finalLine) {
    try {
      $finalResult = ($finalLine | ConvertFrom-Json).result
    } catch {
      $finalResult = 'invalid_final_json'
    }
  }
  $passed = $process.ExitCode -eq 0 -and
            $finalResult -eq 'boundary_telemetry_pass_manual_visual_pending'
  $results += [ordered]@{
    run = $index
    exit_code = $process.ExitCode
    telemetry_result = $finalResult
    passed = $passed
    log = $log
    log_sha256 = $hash
  }
  if (-not $passed) {
    Write-Host "Run $index FAIL；停止本档后续运行，保留日志。" -ForegroundColor Red
    break
  }
  Write-Host "Run $index telemetry PASS。" -ForegroundColor Green
}

$allPassed = $results.Count -eq $Runs -and
             @($results | Where-Object { -not $_.passed }).Count -eq 0
$summary = [ordered]@{
  schema = 'desktopup.ts-h01-d3.dpi-gate-summary.v1'
  recorded_at = (Get-Date).ToString('o')
  scale_percent = [int]$scaleLabel
  expected_dpi = $ExpectedDpi
  expected_cold_starts = $Runs
  completed_runs = $results.Count
  telemetry_pass = $allPassed
  manual_visual_result = 'PENDING_USER_CONFIRMATION'
  runs = $results
}
$summaryPath = Join-Path $runRoot 'summary.json'
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host ""
Write-Host "Summary: $summaryPath"
if ($allPassed) {
  Write-Host "$scaleLabel% / DPI $ExpectedDpi：5/5 telemetry PASS；等待人工视觉确认。" -ForegroundColor Green
  exit 0
}
Write-Host "$scaleLabel% / DPI $ExpectedDpi：telemetry FAIL。" -ForegroundColor Red
exit 4
