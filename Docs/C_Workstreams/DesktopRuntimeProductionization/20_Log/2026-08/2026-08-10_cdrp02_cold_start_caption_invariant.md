# C-DRP-02 冷启动标题栏样式不变量（第二轮）

日期：2026-08-10

状态：`COMPLETE — USER ACCEPTED`

## 用户复现

2026-08-09 的隐藏启动与 ready 顺序修复后，用户在真实首次启动中仍观察到顶部浅色白条；执行收起后白条消失。上一轮 `FIX VERIFIED` 结论因此撤销。

## 现场证据

- 异常现场海报父窗最终 Win32 style 为 `0x14C80000`，包含 `WS_CAPTION`；
- 父窗 client origin 与 outer origin 相同，WebView/Chromium 子窗已完整覆盖 `657×531`，排除 CSS 和 WebView 子窗偏移；
- 说明宿主在隐藏阶段去框成功后，Tauri/tao 窗口生命周期再次写入缓存的顶层窗口 style；
- 收起造成后续窗口重布局，所以表现为“收起后消失”，但启动顺序本身无法保证 style 永久不变。

## 第二轮修复

- 在已安装的原生 poster window subclass 中处理 `WM_STYLECHANGING`；
- 当修改目标为 `GWL_STYLE` 时，直接从 proposed `styleNew` 清除 `WS_CAPTION`、`WS_THICKFRAME`、`WS_SYSMENU`、`WS_MINIMIZEBOX`、`WS_MAXIMIZEBOX`；
- WorkerW/ToolWindow 原有 `WS_CHILD`/`WS_POPUP` 所有权保持不变；
- 原生配置函数复用同一个 `strip_non_client_decoration_styles`，避免一次性配置与消息不变量产生两套规则；
- 添加 Rust 纯函数测试和 Node 架构测试，冻结“后续 style 修改不得恢复原生标题栏”。

## 自动与实机证据

- `pnpm run check`：46 项 Node/契约测试通过；
- `cargo test --manifest-path src-tauri/Cargo.toml`：32 项通过；
- 正式 `cargo tauri build --no-bundle` Release 构建通过；
- 连续 5 次独立冷启动，海报最终 style 均为 `0x14000000`，相较异常现场 `0x14C80000`，非客户区相关位 5/5 为 0；
- 第 6 次普通 Release 实屏截图中顶部白条消失，进程保留供用户复测。

## 用户验收结论

2026-08-10 用户重启最新版 Release，确认首次显示无顶部白条，收起/展开无裁切且按钮命中正常。C-DRP-02 当前门关闭；第二显示器/混合 DPI 等物理矩阵继续延期至 C-DRP-06。
