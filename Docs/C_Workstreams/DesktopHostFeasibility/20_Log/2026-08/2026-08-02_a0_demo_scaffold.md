# A0 Demo 工程骨架记录

日期：2026-08-02

状态：`IN PROGRESS — NATIVE A0 VERIFIED`

## 已发生事实

- Git 仓库已由用户建立，当前实现分支为 `impl/desktop-runtime-foundation-a0`。
- 已参照 TerrainGenerationPlugin 的文档与分支实践建立本项目 Git 策略。
- 已建立无需第三方前端依赖即可运行的交互 Demo，包含海报、游戏图标、拖动、编辑、主题切换、添加项目和暂停表现。
- 已建立 Node 静态构建、文档链接检查、状态模型测试和浏览器截图脚本。
- 已建立 Tauri 2 Rust 外壳、单实例插件、通知区域图标与完整 A0 快捷菜单。
- 已建立独立海报宿主页面和 Windows `ToolWindow` 适配器；海报窗体无边框、透明、置底、无任务栏入口，并与主页生命周期解耦。
- 已建立每张海报默认锁定、解锁后拖动原生窗体、收起时同步缩短原生窗体的 A0 交互。
- 已确认系统 WebView2、Rust/Cargo、Tauri CLI、Visual Studio C++ 工具链与 Windows SDK 可用。
- 已生成 Desktop-Up A0 应用图标与 Release 可执行文件。
- 已把通知区域快捷入口与本地 BYOK AI 助手纳入设计和交付计划。

## 当前验证证据

- JavaScript 语法检查通过。
- 状态模型测试共 7 项，全部通过，其中包含海报锁定安全默认值。
- 文档链接与登记检查对 34 个 Markdown 文件通过。
- 静态前端构建成功并生成 `dist/`。
- Headless Edge 交互冒烟通过：海报可见、动态添加自动排版、主题切换、编辑模式、锁定阻止拖动、解锁拖动和暂停状态均已验证。
- 独立海报页面通过主题状态共享、锁定和收起冒烟；截图见 [`主页 Demo`](assets/a0-demo-home.png) 与 [`独立海报宿主`](assets/a0-demo-home-poster.png)。
- `cargo check`、Debug 构建与 `cargo tauri build --no-bundle` 均通过。
- 版本基线：Node `24.18.1`、用户 pnpm `11.18.0`、Rust/Cargo `1.97.1`、Tauri CLI `2.11.4`、Tauri crate `2.11.5`、Visual Studio Community 2026 `18.7`，并发现 Windows 11 SDK `22621/26100/28000`。
- 原生生命周期闭环通过：`ToolWindow=true`、`AppWindow=false`；关闭主页后进程和海报保持，第二实例自动退出并回呼唯一实例主页。
- 最终 Release 位于 `src-tauri/target/release/desktop-up.exe`，交互式运行工作集观测约 `26.7 MB`；这只是单次观测，不替代后续性能基线。

## 下一步

1. 把当前已验证的独立 bottom-most `ToolWindow` 明确为回退路径，并实现隔离的 WorkerW 桌面挂载适配器。
2. 验证空白区域命中测试、原生位置持久化、多海报窗体和显示器/DPI 恢复。
3. 实现全屏、锁屏、显示器关闭与后台状态的事件驱动暂停，再记录稳定性能基线。
4. 补齐 `TaskbarCreated` 托盘恢复、Explorer 重启恢复和最终 NSIS 安装/卸载链。

代码位置见 [`CODE_ANCHORS.md`](../../00_Catalog/CODE_ANCHORS.md)。
