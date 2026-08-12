# C-PCB-05 契约封板与 Release 候选

日期：2026-08-11

状态：`COMPLETE — AUTOMATION AND USER RELEASE REGRESSION PASS`

## 1. 本轮目标

收敛当前 A0/Release 与正式 `poster-contract-v1` 之间的 legacy 边界，冻结 C-PSS 可依赖的公共输入，确认前端宿主调用没有重新引入兼容旁路，并对主屏单海报 Release 执行核心回归。

本轮不实现 SQLite、迁移事务、模板安装、Shadow DOM 或多海报创建；C-PSS 仍未激活。

## 2. Legacy bridge 收敛

- 新增 `app/src/poster-legacy-bridge.js`，成为 `desktop-up.a0-demo.v1`、`load_poster_snapshot`、`save_poster_snapshot`、A0 两海报演示默认值和第一海报投影辅助函数的唯一前端所有者。
- `app/src/poster-core.js` 只保留渲染与 HTML 转义职责；`main.js`、`poster-surface.js` 和测试均通过显式 legacy 名称访问旧数据。
- bridge 明确区分无版本 `LegacyA0`、带 `contractVersion` 的正式快照和非法输入；正式快照不会降级为 A0，也不会被 localStorage 迁移或旧保存命令覆盖。
- Rust `poster_snapshot.rs` 同样分类 `FormalV1` / `LegacyA0`；任何带 `contractVersion` 的对象只走严格 v1 校验，不允许 legacy fallback。

## 3. 公共契约与 C-PSS 输入

- `poster-contract.js` 导出冻结的 `POSTER_SNAPSHOT_FIELDS`、`POSTER_PERSISTENT_FIELDS`、`POSTER_RUNTIME_ONLY_FIELDS`，作为后继存储字段边界。
- 新增 `tests/poster-contract-seal.test.mjs`，证明 A0 存储键和过渡命令只有一个前端所有者、前端只调用 canonical `sync_host_surface`、模板默认/fallback 常量在 JS/Rust 中各自只有一个权威定义。
- 新增 [`C_PSS_INPUT_CONTRACT_V1.md`](../../10_Plan/C_PSS_INPUT_CONTRACT_V1.md)，冻结正式 schema、默认创建、结构化校验/模板错误、legacy 隔离、宿主边界和 C-PSS 必须自行决定的数据库事项。
- 该输入文档状态为 `FROZEN INPUT — DOES NOT ACTIVATE C-PSS`；SQLite schema、revision/CAS、备份回滚、A0 额外演示海报处理和过渡桥退出时机仍归 C-PSS。

## 4. Release 核心回归发现与修复

首次 Release 检查发现海报可见内容会覆盖前台普通应用。窗口没有 `TOPMOST`，但只锚定 Tauri 返回的外壳 HWND 不足以约束实际可见表面。

一次性原生诊断确认同一海报存在两个顶层 ToolWindow：

- `Tauri Window` 外壳已经位于 `Progman` 前一位；
- `com.allmight.desktopup-*` 可见内容窗口仍位于普通应用层；
- 旧逻辑只缓存和锚定外壳 HWND，因此所有 `SetWindowPos` 虽返回成功，仍没有移动可见内容窗口。

修复后 `desktop_host.rs` 枚举当前进程中可见、顶层、同时带 `WS_EX_TOOLWINDOW` 与 `WS_EX_NOACTIVATE` 的海报表面窗口，排除隐藏托盘和 `Tao Thread Event Target`，按当前 Z 序整组锚定到 `Progman` 之上。控制中心不是该类 ToolWindow，不进入集合。

为覆盖 Tao 启动阶段迟到的窗口创建消息，首次显示后只在 `16/48/96ms` 三个有限延迟点重新枚举并锚定；没有常驻轮询。Shell 临时保护释放和轻量宿主修复也使用同一整组入口。

最终冷启动只读证据：

- Release PID：`10636`，`Responding=true`；
- `Tauri Window` 外壳：Z 序 `485`；
- `com.allmight.desktopup-*` 可见内容：Z 序 `486`；
- `Progman`：Z 序 `487`；
- 可见内容与 `Progman` 严格相邻，普通应用不再被海报覆盖。

一次性补丁、探针和诊断日志已清理；产品源码不保留临时诊断输出。

## 5. 自动门禁

- `pnpm run check`：`89/89` 通过；`113` 个 Markdown 链接有效。
- `cargo test --manifest-path src-tauri/Cargo.toml`：`66/66` 通过。
- `cargo fmt --manifest-path src-tauri/Cargo.toml -- --check`：通过。
- `pnpm run tauri:build -- --no-bundle`：通过。
- Release：`src-tauri/target/release/desktop-up.exe`。
- 构建仅出现既有 MSVC import-library linker message，没有测试或构建失败。

## 6. 待用户确认的主屏单海报核心回归

1. `Win+D` 回到桌面：海报正常显示，无顶部白条、裁切或持续闪烁；连续执行三轮仍稳定。
2. 打开任意普通应用覆盖海报位置：海报必须留在应用后方；再次 `Win+D` 后应回到桌面可见。
3. 桌面空白穿透、卡片点击、标题栏拖动、锁定、收起/展开均无回退，收起前后顶部位置不移动。
4. 托盘双击打开控制中心，关闭并再次打开仍正常；海报显示和 Z 序不受影响。
5. 退出后重新启动当前 Release：首次显示无白条/裁切，普通应用不会被海报覆盖。

## 7. 当前结论

C-PCB-05 的代码、公共输入、自动门禁、Release 构建和冷启动原生 Z 序探针已通过。

2026-08-12，用户确认第 6 节五项主屏单海报核心回归全部通过：连续 Win+D、普通应用覆盖层级、既有交互、控制中心重开和 Release 冷重启均满足预期。C-PCB-05 与 C-PCB 据此关闭。

C-PSS 已满足依赖并取得冻结输入，但尚未激活；不得把本结论解释为 SQLite 已开始实施。

当前发布认证范围仍仅为主屏单海报。第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未在本轮验证。
