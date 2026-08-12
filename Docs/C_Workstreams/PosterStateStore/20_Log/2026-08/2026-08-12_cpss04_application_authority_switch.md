# C-PSS-04 应用权威切换与过渡 bridge 退出

日期：2026-08-12

状态：`COMPLETE — USER RELEASE CORE REGRESSION PASS / C-PSS-05 ACTIVE`

## 1. 本轮结果

- 应用配置目录中的 `poster-state-v1.sqlite3` 已由 Tauri setup 创建并托管，`PosterStateRepository` 成为正式状态仓库；
- Tauri 注册结构化 `load_poster_state`、`replace_poster_state`、`migrate_poster_state` 与 `poster_state_bootstrap_context` 命令；replace 使用 expected revision/CAS，成功写入或迁移后统一发出 `poster-state-updated`；
- 前端新增唯一正式网关 `PosterStateClient`，海报表面和控制中心都通过该网关加载、保存、重置和接收更新，不再各自管理正式状态；
- 启动顺序固定为：宿主确认主工作区 → SQLite load → 仅在空库时检查只读 legacy 来源 → migrate/import → 仅在明确无数据时创建正式默认；
- 正式状态、损坏正式状态或 legacy 拒绝都不会被默认值静默覆盖；CAS 冲突会重载并只重试一次；
- 当前单海报 Release 的六张演示卡片仍是代码内展示内容，不属于正式持久契约；正式持久化只包含 C-PCB 冻结字段。

## 2. Legacy bridge 退出边界

- `poster-snapshot-bridge-v1.json` 与 localStorage 仅能被检查和读取，前端与 Rust 都不再提供 legacy 写命令；
- 原 `load_poster_snapshot/save_poster_snapshot` 正式命令已退出；只保留 `inspect_poster_snapshot` 作为迁移入口；
- shared JSON 明确不存在时才回退检查 localStorage；shared JSON 损坏或非法时 fail closed，不改写来源，也不绕过到下一来源；
- C-PSS-05 通过前不删除 legacy 文件、读取能力或迁移备份。

## 3. 自动化与真实 Release 证据

- JS 全量：`102/102` 通过；
- Rust 全量：`88/88` 通过；
- `122` 份 Markdown 链接有效；
- `cargo fmt --manifest-path src-tauri/Cargo.toml --all -- --check`、`pnpm run build` 与 `pnpm run tauri:build` 通过；
- 最新 Release 成功生成 `src-tauri/target/release/desktop-up.exe` 与 NSIS 安装包；
- 首次真实启动创建 SQLite schema v1，application id 为 `0x44555031`，正式行 `contractVersion=1`、`revision=0`、`selectedPosterId=night-ops`、标题 `NIGHT OPS`；
- 首次迁移保留一份包含完整两海报 A0 envelope 的 SHA-256 定址备份，正式状态仍只有第一张海报；
- 二次真实启动后 revision 仍为 `0`、备份数量仍为 `1`，证明未重复迁移；
- legacy JSON 重启前后 SHA-256 均为 `30B7D6A47B28DAB921B103C37FE01E3E1F1357A7B6B589CB694954BF44DB5B9F`，证明来源未被回写。

## 4. 人工回归中发现并修正

- 用户发现控制中心点击风格预设后会立即回到旧风格；
- 根因是按钮只更新 legacy `theme`，随后正式 SQLite 事件按 `templateKey` 重建 runtime，丢弃非权威 legacy 字段后表现为回弹；
- 三个内置风格现统一映射到正式 `templateKey`：深夜行动使用 `transparent-default`，余烬与玻璃分别使用 `builtin-ember`、`builtin-glass`；
- 控制中心、原生海报与重启恢复现共享同一 SQLite/正式事件链；未恢复 JSON/localStorage 写入；
- 新增“切换后正式刷新不回弹、重启仍保持”的专项测试，并重建、启动最新版 Release。

## 5. 用户人工验收

用户在最新版 Release 确认：

1. `NIGHT OPS` 与六张卡片正常，无顶部白条或裁切；
2. 空白穿透、卡片、拖动、锁定和收起/展开无回退；
3. 改变位置、锁定或收起状态后，通过托盘退出并重新运行，状态恢复正常；
4. 双击托盘打开、关闭并再次打开控制中心正常，海报不受影响且两端状态一致。

上述四项通过，C-PSS-04 正式关闭并激活 C-PSS-05。风格预设回弹修正已有正式刷新和重启恢复自动测试；C-PSS-05 将继续覆盖恢复后的模板状态。
