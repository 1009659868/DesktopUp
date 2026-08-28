# Desktop-Up V2

Desktop-Up V2 是面向 Windows 11 的桌面海报、内容入口与个性化主题平台重构项目。

七份完整需求已冻结，并于 2026-08-28 接受外框几何、内容参数与响应式排版分离修订。项目只使用 Axiom Core 描述模块、端口、组合和物理边界，明确排除 unstable axiom-runtime。用户已接受 Qt 6.8.3 混合生产路线，并在 R4.2 将常驻海报受控修订为 Qt Quick/QML software offscreen renderer + SQLite + 原生 DesktopHost；独立按需 Control Center 继续使用 Qt Quick，两者通过当前用户/当前会话受限 IPC 通信。

文档入口：[Docs/README.md](Docs/README.md)

当前本地分支：`impl/qt-solution`

## 当前权威边界

- 需求：[完整需求索引](Docs/00_Catalog/REQUIREMENTS_INDEX.md)已冻结，海报、运行表面、内容 Item、主题/AI、安全和发布要求均保留；
- 架构：[Qt 方案总执行计划](Docs/A_Project/30_Architecture/QT_SOLUTION_EXECUTION_PLAN_V1.md)是当前实施入口；
- QTS-02：生产物理架构已冻结；
- QTS-03：根级 CMake、正式 `src/`、Runtime、独立 Control Center、SQLite/CAS、受限 IPC、合同与退出夹具已完成；
- QTS-04-01：真实 raised-Progman DesktopHost、layered child manifest、首帧门、generation replacement、恢复回调和退出清理已完成；
- QTS-04-02：NIGHT OPS renderer 已通过 Release 11/11 自动门和用户桌面视觉门，现已关闭；
- QTS-04-03：Host screen geometry、共享手势、split input islands 和 320×420 边界已完成；
- QTS-04-04：生命周期矩阵已通过；R4.2 Qt Quick adapter 与 Host staged-present 已实现并通过 Release 14/14、真实 Runtime smoke 和零资源退出；
- 当前下一步：执行 R4.2 真实桌面 reflow flash 最小门；通过后关闭 QTS-04-04 并进入 QTS-05；
- 尚未通过：本轮布局/拖动人工门和单海报完整 P0；历史候选结果不能替代生产证据；
- QTS-01 的 50 MiB Working Set 门继续 FAIL，性能深化按用户决定延期，但最迟在 QTS-09 前恢复。

## 后续阶段顺序

~~~text
QTS-04-02 单海报 QPainter 视觉
→ QTS-04-03 split input islands
→ QTS-04-04 生命周期、拖动与外框/内容布局收敛
→ QTS-05 StateStore、控制中心与托盘
→ QTS-06 真实 Item 与动态排版
→ QTS-07 主题平台与热更新
→ QTS-08 AI 控制平面
→ QTS-09 发布生命周期与核心回归
~~~

任何阶段都不能把旧实现、候选探针或部分自动结果冒充当前生产 P0。
