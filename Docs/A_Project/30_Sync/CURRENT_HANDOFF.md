# A 层当前交接

最后更新：2026-08-02

状态：`ACTIVE AT A0`

## 已建立

- 产品目标和首版 MVP 边界已形成文档基线；
- Rust + Tauri 2 + Svelte/TypeScript 是当前提议技术栈；
- 安装、恢复和卸载被列为发布硬门；
- A/B/C 活跃链已指向桌面宿主可行性验证；
- Git 仓库已经建立，当前实现分支为 `impl/desktop-runtime-foundation-a0`；
- 已建立零额外依赖的交互前端 Demo 与 Tauri 原生双窗体应用，并完成 Debug/Release 编译；
- 已验证通知区域入口、主页关闭收纳、单实例回呼，以及无任务栏的独立透明海报 `ToolWindow` 回退宿主；
- 已接受 [`运行表面模型 V0.1`](../10_Plan/RUNTIME_SURFACE_MODEL_V0.1.md)：桌面海报是唯一默认可见表面，当前主页只作为按需控制中心；
- 通知区域快捷入口与本地 BYOK AI 助手已进入产品、架构和交付基线。
- [`快捷方式导入与安全收纳模型 V0.1`](../10_Plan/SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md) 已排入 A1：主动导入/扫描后先预览，再由用户选择仅添加或安全收纳；收纳共用恢复账本、撤销与卸载恢复。
- 已完成 A0 表面角色代码对齐：冷启动只显示海报，控制中心由托盘或第二实例按需呼出，关闭后不影响海报。
- 已记录主页与收纳窗后续改为米白、清新风；视觉制作延后到核心功能硬门完成后。
- 已建立 WorkerW 安全适配与 ToolWindow 自动回退诊断；本机无可见 WorkerW，当前采用已验证回退，不强挂隐藏 Shell 窗口。
- 单海报透明边界、按钮/拖动与桌面输入人工矩阵已经通过，C-DHF-03 完成。
- C-DHF-04 已实现原生位置持久化、显示器工作区锚点和显示/Shell 事件恢复基础；重启恢复与模拟离屏拉回通过，真实 Win+D、Explorer、多屏和 DPI 矩阵仍待完成。

## 尚未完成

- 尚未完成 C-DHF-04 真实 Explorer/显示器恢复矩阵与全屏暂停硬门；
- 技术架构仍为 `PROPOSED`，需通过 A0 原型决定；
- 产品名、品牌图标和默认视觉主题尚未冻结；
- 尚未生成 `DesktopUpSetup.exe` 或 `uninstall.exe`。

## 下一动作

命中测试已完成。下一步完成 C-DHF-04 Win+D、Explorer、多屏和 DPI 真实环境矩阵，再进入暂停硬门。精确状态见 [`Desktop Host Feasibility 当前交接`](../../C_Workstreams/DesktopHostFeasibility/30_Sync/CURRENT_HANDOFF.md)。

A1 真实快捷方式能力保持排队状态，在上述 A0 硬门通过前不得开始扫描或移动用户桌面文件。

## 发布底线

最终交付不能只生成开发可执行文件。必须生成可在净机使用的 `DesktopUpSetup.exe`，且安装目录提供独立 `uninstall.exe`，完成快捷方式恢复和应用痕迹清理。
