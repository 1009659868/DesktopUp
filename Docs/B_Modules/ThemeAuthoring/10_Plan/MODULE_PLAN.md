# Theme Authoring 模块计划

最后检查：2026-08-04

状态：`PLANNED`

上层计划：[`DESKTOPUP_V1_IMPLEMENTATION_PLAN.md`](../../../A_Project/10_Plan/DESKTOPUP_V1_IMPLEMENTATION_PLAN.md)

## 1. 责任

拥有 schema 驱动参数面板、预设、素材槽位、图片/SVG/字体净化、Shadow Root 内受限 CSS/HTML 覆盖、主题预览和三种导入导出格式。

## 2. C 工作流

| C 工作流 | 结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| Schema Parameter Authoring (`C-SPA`) | 参数面板、预设和撤销 | C-THP | `PLANNED` |
| Asset Sanitization (`C-ASN`) | 素材净化、限额和槽位 | C-TPS | `PLANNED` |
| Restricted Overrides (`C-ROC`) | CSS/HTML 覆盖和信任边界 | C-SPA, C-ASN | `PLANNED` |
| Theme Import Export (`C-TIE`) | 参数包、素材包和完整模板包闭环 | C-ROC | `PLANNED` |

## 3. 模块完成信号

- 新模板参数无需修改控制中心源码；
- 所有素材和覆盖经过净化、限额和作用域约束；
- 预览、确认和撤销共用统一命令链；
- 导入导出不绕过 Template Platform 安装安全链。
