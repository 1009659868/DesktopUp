# QTS-01 混合 Qt 优化方向记录（2026-08-26）

状态：`USER-ACCEPTED INPUT / DETAIL RESOLVED BY QTS-02 / FUNCTIONAL MAINLINE UNBLOCKED`

关联证据：[QTS-01-01 内存基线审计](QTS_01_01_MEMORY_BASELINE_AUDIT_2026-08-26.md)

## 1. 决定

用户接受“路线三”作为后续内存优化方向：

- Qt 6.8.3 继续作为 Desktop-Up V2 总体实施技术；
- Qt Quick 主要用于控制中心、编辑、预览等按需界面，不再被视为后台海报必须常驻的默认条件；
- 后台常驻海报优先采用原生 DesktopHost 加轻量 renderer；renderer 可以使用 Qt GUI/QPainter、Direct2D、DirectComposition 或原生 D3D11，但具体技术在 QTS-02 生产物理架构冻结时比较和裁决；
- 控制中心关闭后必须释放或终止其 Qt Quick/QML/Scene Graph 资源，具体采用同进程销毁还是独立按需进程，同样由 QTS-02 决定；
- 海报与控制中心继续通过 accepted snapshot、命令/CAS 和 revision 协调，不允许因为 renderer 不同而形成第二状态权威。

这不是对某个具体 renderer API 的提前冻结，也不是立即实施性能优化的授权。当前先恢复功能主线，优化细节留待架构和生产实现具备真实对象后深入讨论。

## 2. 证据基础

QTS-01-01 在同一机器和 Release 工具链下得到：

| 角色 | Working Set 中位平均 |
| --- | ---: |
| Qt GUI，无 Quick | 26.453 MiB |
| 最小 Qt Quick | 63.454 MiB |
| 独立离屏 renderer | 71.881 MiB |
| 当前 Night Ops 单海报 | 136.228 MiB |

最小 Qt Quick 已超过原 50 MiB Working Set 门，而 Qt GUI 基线仍保留约 23.5 MiB 空间。因此，把 Qt Quick 从后台常驻角色移到按需控制中心，是当前最有可能同时保留 Qt UI 生产力和降低后台常驻成本的方向。

## 3. 对主线工作的约束

### QTS-02：生产物理架构

必须明确：

1. 常驻 DesktopRuntime、DesktopHost、轻量海报 renderer 的进程/线程边界；
2. 按需 Qt Quick 控制中心的创建、关闭、崩溃和重复打开合同；
3. snapshot/command/CAS、IPC 或进程内端口以及资源所有权；
4. renderer 替换不改变 DesktopHost 层级、命中、Win+D 和 Explorer 恢复合同；
5. 主题结构化 schema 如何同时服务轻量海报 renderer 和 Qt Quick 控制中心预览。

### QTS-03～QTS-05：骨架、海报与控制中心

- QTS-03 不得把 Qt Quick 链接进所有常驻进程作为默认模板；
- QTS-04 先完成功能正确的轻量单海报生产运行时；
- QTS-05 再接入 Qt Quick 控制中心，并验证关闭后资源释放与状态一致；
- 是否采用独立控制中心进程不在本记录中预判。

### QTS-06～QTS-08：Item、主题和 AI

- Item 活跃度、布局、主题 token 和 AI ChangePlan 必须保持 renderer 中立；
- 用户主题不得依赖把任意 QML/JS 注入常驻海报进程；
- 控制中心可用 Qt Quick 预览，但最终提交仍走统一 snapshot/CAS。

## 4. 保留的未决问题

以下问题延期至 QTS-02 或后续性能检查点，不在本记录中强行决定：

- 海报 renderer 最终采用 QPainter、Direct2D、DirectComposition、原生 D3D11 还是组合方案；
- 动态效果需要持续 renderer，还是允许按变更生成静态快照；
- 控制中心采用独立进程还是同进程按需加载；
- 原 50 MiB Working Set 门是否保持，或在生产实现复测后正式变更；
- QTS-01-02～04 的完整贡献图与优化复测何时恢复。

## 5. 计划处置

- QTS-01-01 保持 `COMPLETE`，原始数据和 50 MiB FAIL 永久保留；
- QTS-01-02～04 转为 `DEFERRED PERFORMANCE CHECKPOINT`，不再阻塞当前功能主线；
- 性能支线最迟必须在 QTS-09 发布生命周期入口前恢复并形成正式预算结论；
- 当前唯一下一动作改为 `QTS-02：Qt 生产物理架构冻结`。
