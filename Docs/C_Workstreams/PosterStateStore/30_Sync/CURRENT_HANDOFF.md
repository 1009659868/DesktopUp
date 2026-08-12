# Poster State Store 当前交接

最后更新：2026-08-12

状态：`ACTIVE — C-PSS-05 RELEASE RECOVERY GATE`

## 已建立

- C-PSS-01 冻结 SQLite schema、application id、CAS、路径、备份和 bridge 退出策略；C-PSS-02 完成结构化 load、Rust v1 校验、原子 replace 与 revision/CAS；C-PSS-03 完成完整 A0 原始备份和第一海报幂等迁移。
- C-PSS-04 已关闭：应用配置目录的 `poster-state-v1.sqlite3` 是正式状态唯一权威，海报表面与控制中心共享 `PosterStateClient`，JSON/localStorage 只保留只读迁移职责。
- 启动顺序固定为宿主主工作区证据 → SQLite load → 空库才检查只读 legacy → migrate/import → 明确无数据才创建默认；任何损坏或拒绝都 fail closed。
- 正式 mutation 使用 expected-revision/CAS；前端冲突时重载并最多重试一次，成功后统一广播 `poster-state-updated`；直接写入错误返回结构化拒绝且不覆盖已接受快照。
- 三个内置风格已映射到正式 `templateKey`，控制中心、原生海报和重启恢复共享同一 SQLite 状态，未恢复 legacy 写入。
- 用户确认最新版 Release 的 `NIGHT OPS` 六卡片、无白条/裁切、桌面空白穿透、卡片、拖动、锁定、收起/展开、退出重启恢复与控制中心往返全部通过。
- 全量 JS `102/102`、全量 Rust `88/88`、文档链接 `123` 份通过；Rust 格式、前端生产构建和原生 Release/NSIS 构建通过。
- 真实 Release 首次迁移创建 schema v1/application id `0x44555031`、revision `0` 的正式状态和一份完整两海报原始备份；重复启动 revision 与备份数量不变，legacy JSON 哈希不变。

## 精确下一步

1. Git 同步检查点 `b447f1f` 已创建；本轮完成相邻交接提交并推送当前实现分支；
2. 同步后继续 C-PSS-05：用临时目录、数据库副本和故障注入执行损坏、未来 schema、外部数据库、不可读/不可写、事务失败和 CAS 冲突矩阵；
3. 验证所有拒绝均可诊断、不会静默默认重置，失败后旧值与 legacy/备份仍保留；
4. 完成恢复后的主屏单海报 Release 核心回归，再决定是否允许删除过渡读取能力；
5. C-PSS-05 通过后关闭 C-PSS，并按 A 路线进入 C-IBC / C-RLC。

## 边界

- 当前发布范围仍为主屏单海报；多显示器、混合 DPI、负坐标、热插拔与多海报流程未认证；
- 故障矩阵不得在无恢复副本时损坏真实用户数据库；
- A0 第二演示海报只存在于原始来源/备份，不进入正式状态；
- `minimal-fallback`、runtime/interaction 与物理窗口状态不得持久化；
- C-PSS-05 关闭前不得删除 legacy 读取能力、备份或原始来源；
- 本地与远端同步基点无分叉；实施检查点为 `b447f1f`，推送后继续 C-PSS-05 隔离恢复矩阵。
