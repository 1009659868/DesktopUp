# C-DRP-01 宿主接口与所有权契约

日期：2026-08-09

状态：`COMPLETE`

## 目标

把 A0 的“先 resize、再设置命中区域”两步窗口写入收敛为一个版本化宿主接口，明确原生宿主拥有窗口几何，海报层只提交逻辑表面、命中区域和位置意图。

## 实现

- 新增 Rust `host_contract.rs`，冻结 `HOST_CONTRACT_VERSION = 1`；
- `HostSurfaceRequest` 包含 `contractVersion`、`revision`、`surfaceId`、`logicalSize`、`scaleFactor`、`placementIntent` 和单海报 `interactionRegion`；
- `HostSurfaceSnapshot` 明确 `geometryOwner = nativeHost`，记录宿主实际 physical size；
- `DisplayTopology` 使用 physical pixel 虚拟桌面坐标，包含显示器 bounds、work area、scale 和 primary 标识；
- 非法版本、NaN/Infinity、越界区域、非法尺寸/圆角和错误 anchor 坐标在原生写入前拒绝；Windows `\\.\DISPLAY1` 等显示 ID 作为 opaque ID 往返；
- 新增 `sync_host_surface`，通过串行协调锁与 revision 防止陈旧请求覆盖新几何；尺寸、placement、region、RuntimeSnapshot 和诊断在同一内部路径更新；
- 前端新增 `host-contract.js`，`poster-surface.js` 只调用一次 canonical sync；WebView 重载时先读取 RuntimeSnapshot revision 再提交下一 revision；
- 旧 `resize_poster_surface` 与 `set_poster_interaction_region` 保留 Tauri 命令签名，但只构造 HostSurfaceRequest 并调用 canonical 内部函数，不再直接写窗口。

## 所有权结论

- Rust 原生宿主是窗口尺寸、物理位置、桌面层级和原生命中区域的唯一写入者；
- Poster Runtime 仍拥有海报业务 width/height/anchor 意图，但只能通过 HostSurfaceRequest 请求宿主应用；
- `poster-snapshot-bridge-v1.json` 不保存 HostSurface/DisplayTopology/RuntimeSnapshot 临时字段；正式海报状态仓库仍属于 C-PSS。

## 自动化验证

- `pnpm run check`：37 项 Node/契约测试通过，88 个 Markdown 文件在最终登记后检查；
- `cargo test --manifest-path src-tauri/Cargo.toml`：24 项 Rust 测试通过；
- `pnpm run build`：通过；
- `cargo tauri build --no-bundle`：通过，生成 Release `desktop-up.exe`；
- `git diff --check`：通过。

## Release 实机证据

- Release PID `77184` 启动后 `Responding=True`；
- canonical sync 后原生海报窗口为 `612×494`，与 `510×412 @1.20x` 的逻辑宿主尺寸一致；
- `GetWindowRgnBox` 为 `588×470`、原点 `(12,12)`，与海报 `490×392`、10px gutter 和 1.20x DPI 一致；
- 海报标题为空，`Style=0x94000000`、`ExStyle=0x8000090`，未恢复 Windows 原生标题栏；
- 启动第二实例后仍只有一个 PID，5 秒 CPU 增量约 `0.0156s`、工作集约 `34.18 MiB`；控制中心随后通过正常关闭请求重新隐藏，进程和海报继续存在。

## 风险与后继

- 当前契约只应用一个海报 interaction region；零到多表面登记与区域合并属于 C-DRP-03；
- 当前生产窗口仍沿用 A0 `poster` 表面，单全局宿主拓扑、统一坐标和窗口枚举不变量由 C-DRP-02 完成；
- legacy 命令只为过渡兼容保留，后继调用不得重新采用两步写入。
