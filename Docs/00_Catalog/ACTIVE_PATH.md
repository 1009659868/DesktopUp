# DesktopUp V2 当前执行入口

最后检查：2026-09-04

当前分支：`impl/poster-redesign-V3`

| 层级 | 权威入口 | 状态 |
| --- | --- | --- |
| 需求 | `REQUIREMENTS_INDEX.md` | `FROZEN — USER REVISION 2026-08-28` |
| 架构 | `../A_Project/20_Architecture/DESKTOPUP_V2_V3_ARCHITECTURE_V1.md` | `V3 DRAFT — DESKBOX-BASED` |
| 模块 | `../A_Project/20_Architecture/DESKTOPUP_V2_V3_MODULE_CATALOG_V1.md` | `V3 DRAFT — PORT BOUNDARIES DEFINED` |
| 制作计划 | `../A_Project/30_Plans/DESKTOPUP_V2_V3_IMPLEMENTATION_PLAN_V1.md` | `V3-04 ACTIVE` |
| 实现记录 | `../A_Project/40_Implementation/V3-04/VERIFICATION.md` | `CURRENT` |
| 代码 | `../../src/` | `V3-04-03 COMMAND/QUERY + THREE-SURFACE PROJECTION` |
| 测试 | `../../tests/DesktopUp.Contracts.Tests/` | `97/97 PASS` |

## 当前动作

V3-03 已由用户完成最终人工裁决并关闭。V3-04-01 已完成：

1. `IStateStore` 位于 Application 端口，`SqliteStateStore` 位于 Infrastructure，不让 UI 直接访问 SQLite；
2. schema v1 建立 migration ledger、单例 state head 和 operation journal；
3. expected revision CAS、operation id 幂等重放和业务 mutation 在同一 SQLite transaction 内提交；
4. 有内容的旧数据库在迁移前先做 SQLite online backup，并验证 `quick_check`、application id、schema 和 revision 后原子落盘；
5. 损坏数据库保留原文件并让启动门失败，不生成第二份默认状态；
6. 应用启动顺序为 state initialization → tray/control center → poster；
7. Release 0 warning / 0 error，真实 SQLite 合同测试 81/81，启动/退出 smoke 为零残留。

V3-04-02 人工门已通过。V3-04-03 已实现 command/query、单事务 aggregate snapshot、Poster/Control Center/Tray 同 revision projection、单调分发、冲突/权限/幂等语义及三端状态入口。当前执行人工三端门；通过后进入 V3-04-04 控制中心桌面预览与编辑。本项不提前实现 V3-05 真实 Item 或 V3-06 热度排版。

## 禁止事项

- 不从 Git 历史恢复旧 Qt/Win32/Axiom 实现；
- 不把 DeskBox 自身测试结果当作 DesktopUp 证据；
- 不在桌面层验证通过前实现主题、AI 或复杂内容；
- 不移动用户文件，直到恢复日志、预演和失败补偿同时存在。
