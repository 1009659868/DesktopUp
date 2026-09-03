# DesktopUp V2 当前执行入口

最后检查：2026-09-03

当前分支：`impl/poster-redesign-V3`

| 层级 | 权威入口 | 状态 |
| --- | --- | --- |
| 需求 | `REQUIREMENTS_INDEX.md` | `FROZEN — USER REVISION 2026-08-28` |
| 架构 | `../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md` | `V3 DRAFT — DESKBOX-BASED` |
| 模块 | `../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md` | `V3 DRAFT — PORT BOUNDARIES DEFINED` |
| 制作计划 | `../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md` | `V3-03 ACTIVE` |
| 实现记录 | `../A_Project/40_Implementation/V3-03/VERIFICATION.md` | `CURRENT` |
| 代码 | `../../src/` | `V3-03 POSTER WINDOW CLUSTER R1 CANDIDATE` |
| 测试 | `../../tests/DesktopUp.Contracts.Tests/` | `52/52 PASS` |

## 当前动作

执行 V3-03 R1 输入、拖缩、锁定、收起与最小滚动人工复测门：

1. NIGHT OPS 单海报可见，USING 无应用外框、系统标题栏、顶部白条、灰框、裁切或未绘制矩形；EDITING 只出现细状态边线；
2. 卡片间、卡片下方等海报空白左拖出现 Windows 桌面框选，右键出现 Windows 桌面菜单；
3. 把原生桌面快捷方式放到海报空白覆盖范围，仍可单击选中、双击启动；
4. 六张测试 Item、顶部标题栏拖动、锁定和收起/展开均正常；
5. 托盘右键选择“进入海报编辑”，右边、底边和右下角可连续缩放；缩短高度后在可见 Item 或右侧滚动轨滚轮上下滚动，六项均可到达；选择“完成海报编辑”后恢复稀疏命中；
6. 锁定后不能拖动/缩放，锁定和收起按钮仍可用；收起/展开保持顶部、左侧和宽度，按钮不跳位；
7. 连续拖动/缩放不丢失 capture，不出现规律闪烁、白条、灰框或抬层；
8. 普通窗口、文件选择器和全屏程序保持高于海报，退出后海报、输入岛、托盘和进程完全消失。

V3-02 R2 已由用户确认完整人工 P0 通过。V3-03 R1 原生命中探针已证明空白点落到 Explorer、Item 点留在 InputIsland，但不代替真实框选、右键菜单、快捷方式、连续手势、滚动与视觉稳定性的人工观察。全部通过后关闭 V3-03，进入 V3-04 权威状态、控制中心与托盘。

## 禁止事项

- 不从 Git 历史恢复旧 Qt/Win32/Axiom 实现；
- 不把 DeskBox 自身测试结果当作 DesktopUp 证据；
- 不在桌面层验证通过前实现主题、AI 或复杂内容；
- 不移动用户文件，直到恢复日志、预演和失败补偿同时存在。
