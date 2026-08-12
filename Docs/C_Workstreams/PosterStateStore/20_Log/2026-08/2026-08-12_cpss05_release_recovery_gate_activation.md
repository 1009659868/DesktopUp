# C-PSS-05 Release 恢复门激活

日期：2026-08-12

状态：`ACTIVE — RELEASE RECOVERY MATRIX`

## 1. 激活依据

- 用户在最新版 Release 确认 `NIGHT OPS` 与六张卡片正常，无顶部白条或裁切；
- 桌面空白穿透、卡片、拖动、锁定和收起/展开均无回退；
- 改变位置、锁定或收起状态后，通过托盘退出并重新运行，SQLite 状态恢复正常；
- 双击托盘打开、关闭并再次打开控制中心正常，海报不受影响且两端状态一致；
- C-PSS-04 因此关闭，SQLite 唯一正式权威与 legacy 只读迁移边界获得主屏单海报 Release 人工验收。

## 2. C-PSS-05 输入基线

- JS 全量 `102/102`、Rust 全量 `88/88`、`123` 份 Markdown 链接有效；
- 最新 Release 与 NSIS 安装包构建通过；
- 正式数据库为 `poster-state-v1.sqlite3`，schema v1、application id `0x44555031`；
- legacy JSON/localStorage、完整迁移备份和只读检查能力继续保留；
- 风格预设已改由正式 `templateKey` 保存，并有正式刷新与重启恢复自动测试。

## 3. 恢复矩阵

C-PSS-05 依次验证：

1. 正常重启、重复启动和既有 revision 加载；
2. JSON 内容损坏、正式契约损坏、元数据不一致与未来 contract version；
3. 外部数据库、未来 schema version、数据库不可读/不可写与事务失败；
4. expected-revision/CAS 冲突、冲突重载和失败后旧值保留；
5. legacy 备份存在、重复迁移幂等、adapter/备份/数据库失败后的来源保留；
6. 恢复后的主屏单海报 Release 视觉、桌面交互、控制中心和持久化核心回归。

## 4. 安全边界

- 故障矩阵优先使用临时目录、数据库副本和测试内故障注入；
- 不在无恢复副本的情况下损坏当前真实用户数据库；
- C-PSS-05 关闭前不删除 legacy 读取能力、原始来源或备份；
- 本门不扩展到多海报 UI、物理多显示器认证、真实快捷方式移动或安装/卸载最终认证。

下一轮按用户要求先执行 Git 同步流程；同步完成后再继续 C-PSS-05 隔离恢复矩阵。
