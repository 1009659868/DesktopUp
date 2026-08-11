# C-DRP-06 稳定态工作集门纠正

日期：2026-08-11

状态：`COMPLETE — USER ACCEPTED PRIMARY-SCREEN SINGLE-POSTER LIMIT`

## 1. 为什么撤回第一切片的性能 PASS

第一版 `performance-gate-v1` 在启动约 4 秒后立即进入四态采样，整个进程约 52 秒结束。它只证明了启动早期窗口：主进程峰值工作集约 32.5 MiB，没有覆盖普通 Release 的长期稳定态。

普通无诊断参数 Release 运行约 7 分钟后，外部复核发现：

- `desktop-up.exe` 总工作集约 121.3 MB，私有内存约 10.2 MB；
- 10 秒内 CPU 与工作集稳定，不符合持续泄漏特征；
- 受控重启后的 0–90 秒主进程稳定约 33.6–34.1 MiB，95 秒升至约 82 MiB，100 秒后稳定约 115.6 MiB；
- 私有内存全过程约 9.5–10 MiB，跃升主要来自 Windows/WebView/DWM 计入主进程的共享驻留页；
- WebView2 完整进程树另有显著占用；本日志不能据主进程私有工作集声称“整个应用低于 50 MiB”。

因此此前早期窗口 PASS 被稳定态证据推翻。按照计划中的字面预算，50 MiB 仍约束 `desktop-up.exe` 主进程总工作集；在用户接受指标或预算调整前不改写为私有工作集。

## 2. 性能门纠正

新增 `performance-gate-v2` / `performance-baseline-v3`：

- 首个样本必须位于启动至少 120000ms 后，否则报告 `stabilization-window-not-reached`；
- 每个模式仍为 6 点、真实 10 秒窗口；
- 继续用主进程总工作集对 50 MiB 做阻断；
- 同时记录主进程私有工作集与私有提交量，解释共享页驻留，但不用于放宽门；
- 旧 `performance-baseline-v2.json` 保留，新证据写入 `performance-baseline-v3.json`；
- 显式 `--cdrp-performance-gate` 仍是唯一诊断入口，普通启动不进入长时间采样。

## 3. 自动化与构建

- `cargo fmt --manifest-path src-tauri/Cargo.toml -- --check`：通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：52/52 通过；
- `pnpm run check`：58/58 通过，99 份 Markdown 链接通过；
- `pnpm run tauri:build`：Release 与 NSIS 通过；
- Release：`src-tauri/target/release/desktop-up.exe`，9,700,864 bytes，SHA-256 `39779F3A47ADB9343A162D5892F6006059DAFA6AA242AB3B95166754F6DB2D6F`；
- NSIS：`src-tauri/target/release/bundle/nsis/Desktop-Up_0.0.1_x64-setup.exe`，2,101,442 bytes，SHA-256 `1BC602559C3EDDD7E83ADD2A81F7ECE41E835777E8A8E483F9E4033F82F2AA2C`。

## 4. performance-gate-v2 的 120 秒稳定态四态结果

| 模式 | 10 秒 CPU 增量 | 峰值总工作集 | 峰值私有工作集 | 峰值私有提交量 | FPS | 结论 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| activeStatic | 0ms | 121,430,016 bytes | 8,167,424 bytes | 10,309,632 bytes | 0 | FAIL：总工作集超 50 MiB |
| activeAnimated 调度状态 | 0ms | 121,413,632 bytes | 8,151,040 bytes | 10,280,960 bytes | 0 | FAIL：总工作集超 50 MiB |
| paused | 16ms | 121,364,480 bytes | 8,101,888 bytes | 10,190,848 bytes | 0 | FAIL：总工作集超 50 MiB |
| stopped | 47ms | 121,364,480 bytes | 8,101,888 bytes | 10,190,848 bytes | 0 | FAIL：总工作集超 50 MiB |

CPU 四态均低于 100ms；静态、暂停和停止为 0 FPS；当前无动画消费者，activeAnimated 调度状态也为 0 FPS。CPU/FPS 门通过；在已被后续用户决策替代的 v2 总工作集口径下，工作集门四态失败。

## 5. 用户确认的指标与启动面决策

2026-08-11 用户确认按可归因的双预算方向继续：

- `desktop-up.exe` 主进程私有工作集预算为 50 MiB；
- Desktop-Up 主进程及其 WebView2 完整后代进程树私有工作集预算为 256 MiB；
- 主进程总工作集、进程树总工作集与私有提交量继续记录，但只作诊断，不作为共享页重复归属下的阻断指标；
- 任一后代进程无法枚举、打开或读取内存时，`process-tree-measurement-incomplete` 必须阻断，不能少算后伪通过；
- 冷启动不创建隐藏控制中心 WebView；控制中心仅在托盘、第二实例、设置或代理入口首次请求时创建。

## 6. performance-gate-v3 / baseline-v4 实现

- `tauri.conf.json` 只静态创建海报窗口；
- `MainWindowCoordinator` 在独立线程串行创建控制中心，PageLoad 完成前的控制事件进入队列，完成后统一发送；
- Windows ToolHelp 快照从当前 PID 递归枚举全部后代，并对每个句柄读取总工作集、私有工作集和私有提交量；
- `performance-gate-v3` 同时裁决主进程 50 MiB 与完整进程树 256 MiB 私有工作集，并把采集完整性纳入硬门；
- 新证据保存为 `performance-baseline-v4.json`，v1..v3 历史文件不覆盖。

## 7. performance-gate-v3 最终结果

| 模式 | 10 秒 CPU 增量 | 主进程总工作集（诊断） | 主进程私有工作集 | 进程树私有工作集 | 进程树私有提交量 | 树进程数 | FPS | 结论 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| activeStatic | 62ms | 33,497,088 bytes | 7,270,400 bytes | 114,192,384 bytes | 218,828,800 bytes | 7 | 0 | PASS |
| activeAnimated 调度状态 | 62ms | 113,823,744 bytes | 7,569,408 bytes | 114,323,456 bytes | 218,882,048 bytes | 7 | 0 | PASS |
| paused | 79ms | 115,113,984 bytes | 7,569,408 bytes | 114,360,320 bytes | 218,447,872 bytes | 7 | 0 | PASS |
| stopped | 62ms | 115,113,984 bytes | 7,569,408 bytes | 114,274,304 bytes | 218,337,280 bytes | 7 | 0 | PASS |

四态首样本均位于启动 120 秒后，每态 6 点、窗口 10.05–10.06 秒；进程树采集完整性全部为 true，违规列表为空。主进程私有工作集峰值约 7.2 MiB，完整进程树私有工作集峰值约 109.1 MiB，分别低于 50/256 MiB。主进程总工作集仍会因共享驻留页升至约 109.8 MiB，但仅作诊断。当前产品没有动画消费者，所以 activeAnimated 调度状态为 0 FPS，不把不存在的视觉动画写成已验证。

## 8. 普通 Release 冷启动懒创建证据

- 冷启动 PID 59068 的树包含 7 个进程、1 个 renderer，树私有工作集 112,869,376 bytes；这证明默认只创建海报 WebView；
- 启动第二实例请求控制中心后，原树变为 8 个进程、2 个 renderer，树私有工作集 154,439,680 bytes；
- 第二实例正常退出，没有产生第二个 Desktop-Up 主进程；新增 renderer 只在首次控制中心请求后出现。

## 9. 当前结论

稳定态性能阻塞已关闭。用户随后在当前最新版普通 Release 上确认以下四项全部通过：

- 海报正常显示，无顶部白条或裁切；
- 空白穿透、卡片、拖动、锁定和收起/展开没有回退；
- 首次双击托盘能正常打开按需创建的控制中心；
- 关闭并再次打开控制中心正常，海报不受影响。

普通 Release 最小烟雾因此标记 PASS。

用户随后决定直接进入 C-PCB，明确接受“当前发布基线只认证主屏单海报”的平台限制。C-DRP-06 据此关闭。第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔仍未认证，不能引用本轮关闭结论声称这些矩阵通过。
