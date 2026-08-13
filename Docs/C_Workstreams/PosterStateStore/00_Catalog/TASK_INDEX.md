# Poster State Store 任务索引

| 任务 | 可验证结果 | 依赖 | 状态 |
| --- | --- | --- | --- |
| C-PSS-01 存储决策与 schema 基线 | SQLite 独立版本、单例快照表、CAS/备份/legacy/退出策略冻结并有内存数据库测试 | C-PCB | `COMPLETE — SCHEMA TESTS PASS` |
| C-PSS-02 加载、原子替换与 CAS | 无数据/合法数据/损坏/版本失败可区分；mutation 原子递增 revision，冲突不覆盖 | C-PSS-01 | `COMPLETE — STRUCTURED LOAD / VALIDATION / CAS PASS` |
| C-PSS-03 Legacy 迁移与原始数据保留 | A0 第一海报幂等迁移，完整 legacy 源保留；失败不默认重置 | C-PSS-02 | `COMPLETE — MIGRATION AND RETENTION PASS` |
| C-PSS-04 应用切换与过渡桥退出 | SQLite 成为唯一正式权威，JSON/localStorage 降为只读迁移输入并按门退出 | C-PSS-03 | `COMPLETE — USER RELEASE CORE REGRESSION PASS` |
| C-PSS-05 Release 恢复门 | 重启、损坏、升级、事务失败和单海报 Release 核心回归通过 | C-PSS-04 | `COMPLETE — AUTOMATED MATRIX AND USER RELEASE REGRESSION PASS` |

C-PSS-01..05 已全部完成，工作流关闭。控制中心 legacy 视觉参数回弹作为后继 Template Platform 参数权威缺陷保留；下一路线为 C-IBC，待用户明确激活。
