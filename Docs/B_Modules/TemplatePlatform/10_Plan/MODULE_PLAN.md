# Template Platform 模块计划

最后检查：2026-08-04

状态：`PLANNED`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有 Shadow DOM 模板运行时、Transparent Default、独立 minimal-fallback、Neon HUD、Cinema Lightbox、热切换、模板净化、完整模板包安装/注册/回滚和卸载引用迁移。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Shadow Runtime and Fallback (`C-SRF`) | Shadow Root 渲染、默认模板和安全 fallback | C-PCB | `PLANNED` |
| Built-in Template Suite (`C-BTS`) | 三套首发模板完整实现和一致性矩阵 | C-SRF, C-PSI | `PLANNED` |
| Template Package Security (`C-TPS`) | 完整模板包净化、预览、原子安装和失败回滚 | C-SRF | `PLANNED` |
| Template Hot-switch Performance (`C-THP`) | 热切换、资源释放、暂停和性能验证 | C-BTS, C-TPS | `PLANNED` |

## 3. 模块完成信号

- 模板结构、样式和覆盖不越出 Shadow Root；
- 默认模板与安全 fallback 角色分离；
- 三模板在目标宽度、DPI、暂停和错误矩阵中通过；
- 安装失败无半注册状态，卸载不产生悬空引用；
- 热切换目标低于 200ms 且不重建 WebView。
