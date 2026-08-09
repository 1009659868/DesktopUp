# Desktop Runtime Foundation 当前交接

最后更新：2026-08-09

状态：`ACTIVE AT B-DRF / C-DRP-02 IN PROGRESS`

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
- C-DRP-02 主屏代码闭环已实现并通过 Release：唯一宿主不变量、monitor/renderer 比例分层、effective logical work area、0.80–1.25 响应策略、CSS 等比呈现和同尺度原生命中均已落地；当前仅剩真实混合 DPI 跨屏物理矩阵，因此仍为 `IN PROGRESS`。

## 下一动作

`C-DHF-01..08` 与 `C-DRP-01` 已 `COMPLETE`。当前唯一活跃工作流仍是 [`C-DRP Desktop Runtime Productionization`](../../../C_Workstreams/DesktopRuntimeProductionization/10_Plan/WORKSTREAM_PLAN.md)，精确下一步为 C-DRP-02 单全局宿主拓扑、统一显示坐标和分辨率/DPI 响应缩放；`C-PCB Poster Contract Baseline` 排在 C-DRP 完成之后。海报 schema、海报内部几何、用户缩放偏好和标题栏语义归属 B-PCR/C-PCB，C-DRP 只定义宿主边界、显示适配与传输契约。

## 不得提前进行

- 不在宿主硬门前构建完整主题编辑器；
- 不直接操作 Explorer 原生桌面图标位置；
- 不在没有恢复策略时移动用户快捷方式。
