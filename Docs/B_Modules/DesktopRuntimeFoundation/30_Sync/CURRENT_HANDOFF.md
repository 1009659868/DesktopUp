# Desktop Runtime Foundation 当前交接

最后更新：2026-08-11

状态：`NEXT — C-DRP COMPLETE / C-RLC READY TO ACTIVATE`

## 当前事实

- 模块边界和接口方向已经形成计划；
- 托盘行为契约已调整：单击托盘不改变海报，双击切换主页，海报显隐仅通过右键菜单“显示海报宿主”控制；
- Tauri 通知区域图标、A0 右键菜单与双窗体生命周期已经通过 Rust/MSVC 编译和交互式运行；
- 通知区域契约已经扩展为主页、设置、显隐、编辑、暂停、快捷主题、自启、桌面重连和退出；
- 独立 bottom-most `ToolWindow` 已通过代码验证，并作为必须保留的回退能力；WorkerW 挂载仍未验证；
- 主页关闭收纳、海报独立保持和单实例回呼闭环已经通过；
- 已接受桌面收纳层默认可见、控制中心/设置/Agent 按需独立的运行表面模型；
- 已通过 Release 生命周期探针验证冷启动仅海报可见、第二实例呼出主页、关闭主页仍保留海报；
- WorkerW 适配器与可观察诊断已经建立；本机没有可见 WorkerW，因此安全采用位于 `Progman` 正上方、普通应用下方的 `ToolWindow` 回退；
- 单海报原生圆角交互区域与透明边界已通过黑盒探针和用户人工矩阵，C-DHF-03 完成；
- 原生位置存储、显示器工作区归一化锚点、合并写盘和显示/Shell 事件恢复基础已实现；重启位置恢复和模拟显示变化拉回通过，C-DHF-04 的 Explorer 真重启、多屏和 DPI 人工矩阵因物理环境延后；
- Release 单次工作集约 `26.7 MB`，性能目标仍需正式场景基线；
- C-DHF-06 四态真实基线采样、机器/采样元数据和 Release 实时数值可视化确认已完成；
- C-DHF-04/05/07 已由用户手动确认通过；
- C-DHF-08 决策封板已完成，`C-DHF-01..08` 全部关闭；
- V1 已冻结单全局宿主、主屏右侧、默认解锁展开和未锁定 USING 自制标题栏拖动契约；这些是目标设计，当前 A0 独立窗体实现尚未对齐和验证；
- 当前模块计划已切换到 [`MODULE_PLAN_V1.md`](../10_Plan/MODULE_PLAN_V1.md)；旧 `MODULE_PLAN.md` 已冻结。模块路线为 C-DHF → C-DRP → C-RLC。
- C-DRP-01 已建立 `host-contract-v1`：Rust 与前端共用 contractVersion/revision/surfaceId/logicalSize/scaleFactor/placementIntent/interactionRegion 字段；RuntimeSnapshot 同步暴露 HostSurface 与 DisplayTopology，原生宿主是唯一窗口几何写入者。
- C-DRP-02 主屏代码闭环、新版 Release 冷启动无顶部白条、收起/展开无裁切与按钮命中已由用户确认；第二显示器/混合 DPI 等物理矩阵继续延期至 C-DRP-06；
- C-DRP-03 单海报视觉/输入分离、桌面空白穿透、卡片/标题栏/锁定交互及收起/展开顶部锚点均已由用户人工矩阵确认，任务关闭；

## 下一动作
- C-DRP-04 最终 ToolWindow Release 的连续 Win+D 已由用户接受通过；
- C-DRP-05 已统一自动/手动暂停、显隐、ready、FPS 与退出，自动验证、Release/NSIS 和用户 5/5 人工矩阵通过，任务关闭；
- C-DRP-06 已按用户确认升级为主进程 50 MiB + 完整进程树 256 MiB 私有工作集双预算，并将控制中心改为首次请求创建；`performance-gate-v3` / `performance-baseline-v4`、58 项前端/契约、52 项 Rust、Release/NSIS 和最新版普通 Release 四项烟雾全部通过；用户随后接受主屏单海报认证限制，任务关闭；

`C-DHF-01..08`、`C-DRP-01..06` 与前置 `C-IBC-01..05` 已关闭。当前发布基线只认证主屏单海报；多显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔未认证。C-RLC 已具备激活输入，但尚未由用户明确激活。

## 不得提前进行

- 不在宿主硬门前构建完整主题编辑器；
- 不直接操作 Explorer 原生桌面图标位置；
- 不在没有恢复策略时移动用户快捷方式。
