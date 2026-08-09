# C-DRP-02 分辨率响应缩放范围调整

日期：2026-08-09

状态：`ACCEPTED / PLANNED`

## 目标

把不同分辨率、Windows DPI 和混合 DPI 显示器下的视觉尺寸一致性正式纳入 C-DRP-02，而不是依赖固定 `490×392` 逻辑尺寸在所有屏幕上产生不同占比。

## 决策

- C-DRP-02 负责从 DisplayTopology 计算显示适配，不把该责任推迟到模板或布局阶段；
- DisplayTopology 区分 physical work area、device scale 和 effective logical work area；
- 设计尺寸、响应缩放和用户缩放使用三个独立概念：
  - design size：海报业务基准尺寸，不因换屏而改写；
  - responsive scale：宿主根据目标显示器有效逻辑工作区计算；
  - user scale：后继业务偏好，当前固定 `1.0` 并仅预留接口；
- 原生物理尺寸按 design size、responsive scale、user scale 与 device scale 的组合结果生成；命中区域必须使用同一有效缩放；
- 初始验证基线为 `1920×1080 logical = 1.0`，候选范围 `0.80–1.25`；具体曲线、阈值、舍入和滞回策略在 C-DRP-02 的测试矩阵中冻结。

## 验证范围

- 有效工作区：1366×768、1920×1080、2560×1440、3840×2160；
- Windows DPI：100%、125%、150%、200%；
- 等效组合：例如 4K@150% 与相近有效逻辑工作区应得到等价视觉尺度；
- 跨屏组合：主屏切换、混合 DPI、负坐标副屏和任务栏工作区变化；
- 行为：展开/收起、命中区域、归一化锚点、重启恢复、无裁切、无累计漂移、无 resize 循环；
- 所有自动化通过后仍需 Release 真实桌面探针；缺少物理显示器时保留明确待测项。

## 边界

- 本次没有实现响应缩放代码；
- C-DRP-02 不持久化用户偏好、不修改海报 schema、不决定图标重排；
- 用户主动调整海报框尺寸和内部响应式布局仍归 C-PCB/C-PSS 与后继 Poster Core/Layout 工作流；
- C-DRP-04 继续负责显示器/DPI 变化后的恢复协调，但使用 C-DRP-02 冻结的坐标和缩放模型。

## 下一步

先在 C-DRP-02 建立单宿主不变量和 DisplayTopology 的 effective logical work area，再实现纯函数缩放策略及矩阵测试，最后接入原生宿主尺寸、interaction region 和跨屏重算。
