# Poster Core Runtime 模块计划

最后检查：2026-08-14

状态：`ACTIVE — C-PSI`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有海报状态 schema、版本化 SQLite 状态仓库、默认快照、主屏右侧几何、自制 DOM 标题栏、大幅面透明表面、展开/收起、USING/EDITING、锁定、命中区域、暂停同步，以及接入正式 Item 与真实启动后的整合 Release 验收。

不拥有模板包安装、快捷方式文件事务或排序算法。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Poster Contract Baseline (`C-PCB`) | 状态 schema、几何所有权、默认快照、标题栏和失败回退契约测试 | C-DRP | `COMPLETE` |
| Poster State Store (`C-PSS`) | 版本化 SQLite schema、仓库接口、迁移和过渡 JSON 桥接替换 | C-PCB | `COMPLETE — USER RELEASE RECOVERY PASS` |
| [`Large Surface and Transparency`](../../../C_Workstreams/LargeSurfaceTransparency/10_Plan/WORKSTREAM_PLAN.md) (`C-LST`) | 主屏右侧大幅面透明海报与位置恢复 | C-PSS, C-DRP | `COMPLETE — ABSORBED AND AUDITED` |
| [`Poster State and Interaction`](../../../C_Workstreams/PosterStateInteraction/10_Plan/WORKSTREAM_PLAN.md) (`C-PSI`) | 状态机、锁定、标题栏拖动、海报框拉伸与最小/最大尺寸限制、命中和暂停同步 | C-LST | `ACTIVE — C-PSI-02 AUTHORITY` |
| Poster + Item Integration Release Gate (`C-PCRG`) | 使用正式 Item 仓库和真实启动链执行 Debug/Release、Windows 矩阵和核心性能封板 | C-PSI, C-LCH | `PLANNED AFTER USABLE MVP` |

## 3. 模块完成信号

- 新海报从 `primary/top-right/EXPANDED/USING/ACTIVE_STATIC/unlocked` 唯一默认快照创建；
- 未锁定 USING 可拖自制标题栏，控件排除；
- 几何只有一个权威所有者，显示器变化后可恢复；
- SQLite schema、迁移与回滚/保留策略经过幂等测试，过渡 JSON 不再作为权威状态源；
- C-LST 已通过吸收审计；C-PSI 先审计并收敛可承载 Item 的稳定交互表面，随后由 C-CIM/C-ROD/C-PAA/C-LCH 接入正式数据与启动；
- 最终以正式 Item 仓库而非硬编码 `GAME_CATALOG` 通过透明、命中、暂停、启动和 Release 实机矩阵。

C-PSS、C-LST 与 C-PSI-01 已关闭；控制中心 legacy 视觉参数回弹归后继 Template Platform 参数权威，不重新打开 C-PSS。当前执行 C-PSI-02，只实现审计确认的状态权威真实缺口。
