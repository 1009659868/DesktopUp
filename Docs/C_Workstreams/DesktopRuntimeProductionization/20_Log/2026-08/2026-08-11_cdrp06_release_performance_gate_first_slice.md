> [!WARNING]
> 本日志的启动早期性能 PASS 已被 120 秒稳定态复测推翻；当前权威结论见 [稳定态工作集门纠正](2026-08-11_cdrp06_stable_working_set_correction.md)。

# C-DRP-06 Release、性能与后继接口门第一切片

日期：2026-08-11

状态：`IN PROGRESS — AUTOMATED / RELEASE PERFORMANCE PASS`

## 1. 本轮目标

把 C-DRP-01..05 的生产宿主实现收敛为可重复判定的 Release 门：Debug/Release 样本必须声明相同 Host Contract 版本；静态、暂停、停止和动画调度状态使用严格 10 秒窗口；CPU、工作集和 FPS 预算由版本化报告自动裁决，不能再靠控制中心读数或人工估算。

## 2. 入口审计结论

原 C-DHF 性能采样不能直接作为 C-DRP-06 完成证据：

- “10 秒”实际只在 `t=0/2/4/6/8s` 采 5 点，窗口约 8 秒；
- `cpuMs` 是进程累计 CPU 时间，没有计算窗口增量，也没有自动与 `0.10s` 预算比较；
- 控制中心以 `192 MiB` 着色，而 C-DRP/A 层硬门为 `50 MiB`；
- `activeAnimated` 的旧 FPS 采样循环未限速，历史基线曾记录 60 FPS；
- `performance-baseline-v1.json` 只有原始样本，没有版本化 PASS/FAIL、Release profile 或 Host Contract 版本证据；
- 当前静态海报没有真实动画消费者，不能把“必须观察到非零 FPS”作为完成条件。正确条件是存在动画帧时不得超过 30 FPS，0 FPS 合法。

## 3. 已实施

- 新增 Rust `performance-gate-v1`：每态 6 点、窗口至少 9500ms，静态/暂停 CPU 增量不高于 100ms，四态工作集不高于 50 MiB，静态/暂停/停止必须 0 FPS，动画调度状态只约束上限 30 FPS；
- 样本新增 `buildProfile`、`hostContractVersion`，Debug 样本或 Host Contract 版本不一致时不得认证 Release；
- 基线升级为 `%APPDATA%/com.allmight.desktopup/performance-baseline-v2.json`，保存 `performance-gate-v1` 报告和四态原始样本，不覆盖历史 v1 文件；
- 控制中心改为 6 点覆盖真实 10 秒，并把工作集提示阈值对齐到 50 MiB；
- 海报动画调度探针改为 `setTimeout + requestAnimationFrame` 的最高 30 FPS 调度；暂停/停止同时取消 timer 与 rAF；当前无动画消费者时仍保持 0 FPS；
- 新增显式 `--cdrp-performance-gate` Release 诊断模式：普通启动不进入；诊断启动由原生层依次采四态、保存报告并以 0/2 退出码表示 PASS/FAIL，不开放生产 WebView 调试端口；
- `pnpm run probe:performance-gate` 启动该诊断模式并拒绝陈旧基线。

## 4. 自动与 Release 证据

- `pnpm run check`：58/58 通过，97 份 Markdown 链接通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：50/50 通过；
- `cargo fmt --manifest-path src-tauri/Cargo.toml -- --check` 通过；
- `pnpm run tauri:build` 通过；
- Release：`src-tauri/target/release/desktop-up.exe`，9,689,600 bytes，SHA-256 `9A363C19E70047249DCB7B07D7603F1A0E687159AF809053BAB595BF4C8305CA`；
- NSIS：`src-tauri/target/release/bundle/nsis/Desktop-Up_0.0.1_x64-setup.exe`，2,095,336 bytes，SHA-256 `B118BF957327018C0C2E25E0C4968A2069D816FEFF97344FE9CE8C9F2C135A79`；
- 普通无诊断参数 Release 已恢复启动，PID 62320，进程响应正常。

性能环境：Windows 11 专业版 `10.0.26100`，Intel Core i7-14700（20 核/28 逻辑处理器），可见内存 33,203,952 KiB。最终四态门 `PASS`：

| 模式 | 窗口 | CPU 增量 | 峰值工作集 | FPS | 结论 |
| --- | ---: | ---: | ---: | ---: | --- |
| activeStatic | 10004ms | 0ms | 34,115,584 bytes | 0 | PASS |
| activeAnimated 调度状态 | 10003ms | 32ms | 34,115,584 bytes | 0 | PASS；当前无动画消费者，仅适用 30 FPS 上限 |
| paused | 10003ms | 0ms | 33,714,176 bytes | 0 | PASS |
| stopped | 10003ms | 0ms | 33,714,176 bytes | 0 | PASS |

第一轮报告曾仅因 `activeAnimated:animated-fps-not-observed` 失败。该规则与当前静态产品事实冲突，修正为“0 FPS 合法、超过 30 FPS 失败”后增加回归测试并复跑同一实测门；CPU、工作集、窗口长度和状态一致性在两轮均未超标。

## 5. 稳定接口与兼容边界

- C-PCB 正式入口为 `host-contract-v1`、canonical `sync_host_surface`、`runtime-lifecycle-v1`/`runtime-state-changed`、RuntimeSnapshot 中的 HostSurface/DisplayTopology/HostTopology/DesktopHost diagnostics；C-PCB 不得访问 WorkerW、Progman、Win32 HWND、DPI API 或输入钩子；
- `resize_poster_surface`、`set_poster_interaction_region` 和 `runtime-pause-state` 只保留为 A0 兼容包装/事件，不允许新增调用；它们必须在 C-PCB 行为测试全部切到 canonical 接口后、最迟 C-PSS 集成门前移除；
- `poster-snapshot-bridge-v1.json` 仍是过渡状态桥接，只能在 C-PSS 完成 SQLite 迁移、回滚和幂等验证后退出权威路径。

## 6. 尚未关闭

- 用户需对普通 PID 62320 做最小烟雾确认：首次海报可见、无顶部白条，收起/展开与锁定按钮正常，桌面空白穿透没有明显回退；
- 当前机器检测到两屏：主屏 `DISPLAY2` 为 2560×1440，工作区 2560×1392；副屏 `DISPLAY1` 位于正坐标 `(2560,243)`，为 1920×1200，工作区 1920×1152；
- 根据此前用户决定，本轮未执行混合 DPI、主屏切换、跨屏重启和热插拔，也没有把当前正坐标副屏改为负坐标。C-DRP-06 关闭前必须实际补测，或由用户明确接受“当前发布基线只认证主屏单海报，多屏/DPI/热插拔未认证”的发布限制。

C-DRP-06 因此保持 `IN PROGRESS`，不能仅凭自动性能 PASS 关闭。
