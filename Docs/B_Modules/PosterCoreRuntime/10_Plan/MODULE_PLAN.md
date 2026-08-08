# Poster Core Runtime 模块计划

最后检查：2026-08-04

状态：`PLANNED NEXT`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有海报状态源、默认快照、主屏右侧几何、自制 DOM 标题栏、大幅面透明表面、展开/收起、USING/EDITING、锁定、命中区域、暂停同步和核心 Release 验收。

不拥有模板包安装、快捷方式文件事务或排序算法。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Poster Contract Baseline (`C-PCB`) | 单状态源、几何所有权、默认快照、标题栏和失败回退契约测试 | C-DHF | `PLANNED NEXT` |
| Large Surface and Transparency (`C-LST`) | 主屏右侧大幅面透明海报与位置恢复 | C-PCB, C-DRP | `PLANNED` |
| Poster State and Interaction (`C-PSI`) | 状态机、锁定、标题栏拖动、海报框拉伸与最小/最大尺寸限制、命中和暂停同步 | C-LST | `PLANNED` |
| Poster Core Release Gate (`C-PCRG`) | Debug/Release、Windows 矩阵和核心性能封板 | C-PSI | `PLANNED` |

## 3. 模块完成信号

- 新海报从 `primary/top-right/EXPANDED/USING/ACTIVE_STATIC/unlocked` 唯一默认快照创建；
- 未锁定 USING 可拖自制标题栏，控件排除；
- 几何只有一个权威所有者，显示器变化后可恢复；
- 透明、命中、暂停和 Release 实机矩阵通过。
