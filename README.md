# DesktopUp V2 — Poster Redesign V3

当前分支：`impl/poster-redesign-V3`

状态：`V3-01 IMPLEMENTED / AUTOMATED GATE PASS / MANUAL GATE PENDING`

本分支已经移除旧 Qt、Win32 探针、Axiom 试验、生产代码和旧测试集。冻结需求继续保留；新的技术路线以开源 DeskBox 的 C#、WinUI 3、Windows App SDK、原生 Shell 边界和窗口生命周期实践为主要参考，并针对 DesktopUp 的透明海报、内容 Item、活跃度排版、主题热更新与 AI 控制平面进行扩展。V3-01 已建立五层工程、单实例启动意图、当前用户数据目录、结构日志、资源台账、最小托盘与按需控制中心。

## 阅读顺序

1. [完整需求索引](Docs/00_Catalog/REQUIREMENTS_INDEX.md)
2. [V3 技术路线与总体架构](Docs/A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md)
3. [V3 模块分类与端口目录](Docs/A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md)
4. [V3 制作计划](Docs/A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md)
5. [V3-01 实现与验证](Docs/A_Project/40_Implementation/V3-01/VERIFICATION.md)
6. [当前执行入口](Docs/00_Catalog/ACTIVE_PATH.md)

## 参考与许可证边界

- 参考工程：`G:\A_\Tools\reference\DeskBox`；
- 当前参考源码声明为 `GPL-3.0-only`；
- V3 可以直接借鉴或移植其实现，但任何直接衍生代码都必须保留来源和许可证通知；如果分发衍生版本，应履行 GPLv3 的对应源码义务；
- DeskBox 名称、图标、品牌和发行包装不属于 DesktopUp 资产，不得复制为 DesktopUp 品牌；
- V3-01 未逐字复制 DeskBox 源文件，但已采用其依赖组合和托盘/生命周期构造模式并登记来源；
- 本探索分支以 `GPL-3.0-only` 发布，完整文本见 [LICENSE](LICENSE)，依赖通知见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 构建与运行

```powershell
& "G:\A_\Tools\DesktopUp_V2\scripts\build.ps1" -Configuration Release
& "G:\A_\Tools\DesktopUp_V2\scripts\test.ps1" -Configuration Release
& "G:\A_\Tools\DesktopUp_V2\scripts\run.ps1"
```

普通首次启动只常驻托盘；双击托盘打开唯一控制中心，右键托盘可退出。当前尚无海报窗口，桌面宿主从 `V3-02` 开始。
