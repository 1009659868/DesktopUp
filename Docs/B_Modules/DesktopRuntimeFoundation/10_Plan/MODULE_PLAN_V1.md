# Desktop Runtime Foundation V1 模块计划

最后检查：2026-08-09

状态：`ACTIVE`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有单全局透明宿主、桌面挂载与 ToolWindow 回退、Shell/显示器/DPI 恢复、有效工作区响应缩放、事件驱动暂停、托盘、单实例、控制中心生命周期、自启和宿主性能。

不拥有海报模板、快捷方式收纳、排序算法、主题编辑或 AI。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Desktop Host Feasibility (`C-DHF`) | 关闭宿主可行性、恢复、暂停、性能和托盘硬门 | 无 | `COMPLETE` |
| Desktop Runtime Productionization (`C-DRP`) | 把原型收敛为单全局生产宿主和稳定接口 | C-DHF | `ACTIVE` |
| Runtime Lifecycle and Quick Access (`C-RLC`) | 单实例、控制中心、托盘、自启、退出和 TaskbarCreated 恢复 | C-DRP | `PLANNED` |

## 3. 当前任务顺序

`C-DHF-01..08 COMPLETE → C-DRP ACTIVE → C-RLC`

## 4. 模块完成信号

- 单全局宿主及回退在真实 Windows 矩阵中可诊断、可恢复；
- 分辨率响应缩放与 Windows DPI 分层计算，1080p/2K/4K 和混合 DPI 下不双重缩放、不裁切、不改写海报业务尺寸；
- 透明区域不吞桌面输入，使用态不抢焦点；
- 全屏、锁屏、熄屏暂停无持续渲染；
- 托盘、单实例、控制中心、自启和退出生命周期通过；
- 对 Poster Core 暴露稳定的几何、命中、可见性和暂停契约。

## 5. 不得提前

- 不把 A0 独立海报窗体直接当作生产单全局宿主；
- C-DHF-08 已关闭，但 A0 独立海报窗体仍需通过 C-DRP 收敛为生产单全局宿主；
- 不扫描或移动真实桌面文件。
