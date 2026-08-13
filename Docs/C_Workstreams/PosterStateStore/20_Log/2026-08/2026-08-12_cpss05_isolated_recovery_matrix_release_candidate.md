# C-PSS-05 隔离恢复矩阵 Release 候选

日期：2026-08-12

状态：`AUTOMATED RELEASE MATRIX PASS — USER CORE REGRESSION PENDING`

## 1. 本轮结果

C-PSS-05 新增一个只由显式诊断参数触发的 Release 恢复门。探针在 `tauri::Builder` 和单实例插件初始化之前运行并退出，不创建正常应用窗口、不连接正式应用配置目录，也不接触当前用户的正式 SQLite、legacy 来源或备份。

Release 探针在系统临时目录内创建独立 schema v1 数据库、注入故障并输出原子 JSON 报告。Node 驱动器持有临时目录生命周期，清理前同时校验临时目录直属系统临时根且名称带固定前缀，避免扩大删除范围。

## 2. 恢复矩阵

实际 Release 报告契约为 `poster-state-recovery-gate-v1`，以下 `8/8` 场景通过：

1. `restart-revision-and-schema-bootstrap`：空库建立 schema v1，revision `0 → 1`，两次重开后值仍可恢复；
2. `damaged-content-rejections`：损坏 JSON、非法正式契约、未来 contract version、元数据不一致均结构化拒绝，未触发默认重置；
3. `schema-and-database-identity-rejections`：未来 schema 与外部 application id 均拒绝且原 pragma 未被改写；
4. `unreadable-and-query-failures`：不可作为数据库打开的路径与表缺失分别返回结构化数据库错误；
5. `cas-conflict-and-validation-preserve`：expected-revision 冲突与非法 mutation 均保留 revision `0` 的旧值；
6. `transactional-write-failure-rollback`：触发器注入 UPDATE 失败后事务回滚，旧 revision 与旧值保留；
7. `legacy-backup-and-idempotence`：正式状态只迁移第一海报，备份保留两海报完整来源，重复迁移后仍为一份备份/revision `0`；
8. `legacy-failure-source-retention`：adapter、备份目录和数据库写入失败均不创建正式状态；可建立备份的失败路径保留原始来源，三类故障解除后均可重新迁移并恢复 revision `0`，且不重复生成备份。

## 3. 自动验证证据

- C-PSS-05 Rust 专项：`1/1` 通过；
- Rust 全量：`89/89` 通过；
- JS/运行时全量：`105/105` 通过；
- Rust 格式检查通过；
- 文档更新后的链接门：`124` 份通过；
- `pnpm run tauri:build` 通过，生成新版 `desktop-up.exe` 与 NSIS 安装包；
- `pnpm run probe:poster-state-recovery-gate` 的真实 Release 报告 `passed=true`、`violations=[]`、`8/8` 场景通过；
- 探针执行前后对正式应用配置目录全部 `10` 个文件执行路径、长度和 SHA-256 对比，结果完全一致；
- 探针临时目录已清理，正常 Release 已重新启动。

## 4. 代码与运行边界

- `src-tauri/src/poster_state_recovery_gate.rs` 持有恢复场景、结构化报告和故障注入；
- `src-tauri/src/lib.rs` 只在显式 `--cpss-recovery-gate-report=...` 参数存在时走前置诊断分支；
- `scripts/run-poster-state-recovery-gate.mjs` 创建并验证临时目录、覆写隔离的 `APPDATA/LOCALAPPDATA`、启动 Release、校验报告并安全清理；
- `tests/poster-state-recovery-gate.test.mjs` 封住前置顺序、场景集合、生产 API 使用和临时目录安全约束；
- 正常 Release 路径、正式命令、SQLite 权威、legacy 只读来源与现有备份均未删除或改写。

## 5. 尚未关闭的人工门

C-PSS-05 暂不标记完成。用户需在当前已启动的新版正常 Release 上确认：

1. `NIGHT OPS` 和六张卡片正常显示，无白条、裁切或异常诊断窗口；
2. 空白穿透、卡片点击、标题栏拖动、锁定、收起/展开无回退；
3. 改变位置、锁定/收起或风格后，通过托盘退出并重新运行，SQLite 状态恢复；
4. 双击托盘打开控制中心，关闭并再次打开，两端状态一致且海报不受影响。

上述四项通过后，才可把 C-PSS-05 与 C-PSS 标记完成并切换到 A 路线中的 C-IBC / C-RLC。当前发布认证仍只覆盖主屏单海报；多海报和物理多显示器矩阵未认证。
