# TS-H01-E5 诊断事件与候选一致性夹具

状态：`COMPLETE — E5 CONTRACT AND FIXTURE FROZEN`

日期：2026-08-21

## 1. 目的

E5 将 E2-E4 的能力、生命周期、几何、命中和渲染合同变成所有候选都必须输出的事件 envelope、场景清单和离线校验规则。夹具位于 `prototype/desktop-host-win32/ts-h01-e5/`；它不依赖候选框架，也不创建桌面窗口。

夹具自测通过只证明 schema/validator 能识别一组已知 PASS 和已知 FAIL 日志；它不证明 TS-C01 至 TS-C07 的任何候选已经运行或通过 P0。视频、人工层级/输入观察和性能采样仍不可由 JSONL 校验替代。

## 2. 事件 envelope

每行一个完整 JSON object，schema 固定为 `desktopup.desktop-host.event.v1`。所有事件必须含：

```text
schema, event, monotonic_ms, sequence,
host_session_id, candidate_id, build_id, result
```

涉及 attachment/surface/projection 的事件还必须含适用身份：

```text
attachment_id, shell_generation, lifecycle_epoch, surface_epoch,
host_projection_revision, source_snapshot_revision,
hit_region_revision, render_revision, display_environment_revision
```

`sequence` 在一个 HostSession 内从 1 严格连续；`monotonic_ms` 不回退；candidate/build/session 不能在文件中途改变。结构化原始事件不得只保留最终摘要。

## 3. 冻结事件族

| 事件 | 最低语义 |
| --- | --- |
| `host_session_started` | session/build/candidate 起点、OS 与时钟基准 |
| `capability_snapshot` | 能力逐项 disposition、环境范围和 Evidence ID |
| `topology_discovered` | topology、Explorer/Shell generation、安全目标唯一性 |
| `operation_started/completed/preempted` | RequestId、operation kind、epoch、幂等/取代关系 |
| `attachment_state` | HostState、SurfaceState、parent/owner/z/visible/input gate |
| `projection_committed` | source/host/display/hit/render expectation 及逻辑/物理边界 |
| `surface_ready` | transport、ownership、surface epoch 和 target bounds |
| `frame_presented` | 当前 frame 对应的 projection/render revision 与 pixel bounds |
| `shell_generation_lost` | old generation、fence 和 degraded reason |
| `degraded_entered` | 不可见/不可输入且无普通顶层 fallback |
| `recovery_started/completed` | RecoveryCycleId、cause、budget、结果和新 generation |
| `surface_fault` | device/transport fault 与 surface-only 或 full recovery 分流 |
| `resource_ledger` | host/adapter/quarantine 资源计数与所有权 |
| `cleanup_completed` | attachment/session 清理结果与零资源核对 |
| `host_session_exiting/ended` | 粘滞退出、迟到事件处置和最终清理 |

候选可以增加 namespaced 字段与事件，但不得重定义公共字段或把关键事实只放入私有文本 message。

## 4. 机器硬门

当前 validator 至少拒绝：

- JSONL 无法解析、schema/必需字段缺失、sequence 不连续或时钟回退；
- 同一日志中 candidate/build/HostSession 身份漂移；
- 任一事件出现 `topmost=true` 或 `ordinary_top_level_fallback=true`；
- 同 Shell generation 同时存在两份 live attachment；
- READY/PRESENTED 使用旧 lifecycle/surface/projection revision；
- `frame_presented` 的 render revision、hit revision或 pixel bounds 不匹配当前 projection；
- host_session_ended 未经过 exiting，或最终 resource ledger 非零；
- 将 `UNKNOWN/NOT_RUN/DEFERRED/USER_ACCEPTED_WITH_EXCEPTION` 自动提升为 PASS。

validator 的硬门是必要条件，不是充分条件。透明白条、灰框、可见闪烁、原生桌面菜单/框选和快捷方式启动仍需矩阵要求的截图、录屏与人工裁决。

## 5. 共享场景清单

`conformance-manifest.json` 冻结候选必须映射的最小场景：

1. visible/hidden/STOPPED 冷 attach 与首帧；
2. 零/一/多/重叠 hit regions；
3. 20 次 drag、resize、collapse/expand 与 lock rejection；
4. keyboard/taskbar Win+D 各 10 轮，手势设备不可用保持 UNKNOWN；
5. Explorer generation 重建 10 轮及 duplicate reconnect；
6. device/transport loss、stale callback、detach 和粘滞 exit；
7. P0 声明范围内的 DPI、分辨率、work area；
8. Explorer、浏览器、文件选择器、控制中心和全屏程序覆盖；
9. Night Ops 视觉、透明合成、renderer bounds、性能和资源账本。

每条场景映射统一矩阵 ID；运行次数不足、工具失败或环境不具备时只能记 `UNKNOWN/NOT_RUN`，不能借公共宿主证据填 PASS。

## 6. 使用与证据输出

```powershell
pwsh -NoProfile -File .\prototype\desktop-host-win32\ts-h01-e5\validate-desktop-host-events.ps1 `
  -Path <candidate-events.jsonl>

pwsh -NoProfile -File .\prototype\desktop-host-win32\ts-h01-e5\test-validator.ps1
```

候选运行日志放入 `Docs/A_Project/20_Technical_Selection/Evidence/TS-Cxx/<matrix-id>/<run-id>/`。同一 run 还需 `record.md`、environment、reproduce、媒体/哈希和原始采样。重试新建目录，不覆盖失败证据。

## 7. 夹具自测结论

- `samples/pass-minimal.jsonl` 必须返回 0；
- `samples/fail-late-frame.jsonl` 必须返回非 0，并识别 stale projection/frame；
- `test-validator.ps1` 只有同时满足上述正反例才返回 0。

该自测结果登记为 `FIXTURE SELF-TEST PASS`，明确不同于 `CANDIDATE P0 PASS`。

## 8. 退出门

E5 完成条件：公共 envelope、事件族、矩阵 manifest、validator、正反样本和自测命令均存在并可复跑；E2-E4 的关键 fence 可以从日志反证违规；文档明确自动与人工证据边界。满足后进入 E6 候选交接，仍不产生框架赢家。
