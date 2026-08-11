# C-PSS 输入契约 V1

日期：2026-08-11

状态：`FROZEN INPUT — DOES NOT ACTIVATE C-PSS`

## 1. 用途

本文是 C-PCB 交给 C-PSS 的输入清单。它冻结 C-PSS 可以依赖的正式 schema、校验、默认创建、legacy 隔离和失败边界，但不提前设计或实现 SQLite 表、迁移器与仓库。

发生冲突时以代码和共享判定样例为准；本文不得成为第二套校验器。

## 2. 权威代码入口

- `app/src/poster-contract.js`：正式 `poster-contract-v1`、公开字段白名单、默认创建和 A0 到 v1 的显式 adapter。
- `src-tauri/src/poster_contract.rs`：Rust 同构严格校验。
- `app/src/poster-geometry.js` / `src-tauri/src/poster_geometry.rs`：工作区派生默认几何。
- `app/src/poster-template-resolution.js` / `src-tauri/src/poster_template_resolution.rs`：正常模板与临时 fallback 解析。
- `app/src/poster-legacy-bridge.js`：当前 A0 localStorage 与过渡 JSON 命令的唯一前端入口，只能作为迁移输入。
- `src-tauri/src/poster_snapshot.rs`：过渡 JSON 文件存储；只显式分类 `FormalV1` 和 `LegacyA0`，不是 C-PSS 最终仓库。

## 3. 正式持久快照

Envelope 只允许以下字段：

| 字段 | 约束 | C-PSS 含义 |
| --- | --- | --- |
| `contractVersion` | 必须等于整数 `1` | 数据契约版本，不等同于 SQLite schema 版本 |
| `revision` | `0..Number.MAX_SAFE_INTEGER` 的整数 | 持久快照修订号；数据库递增/CAS 细则由 C-PSS 冻结 |
| `selectedPosterId` | 必须引用现有 Poster | 当前选择；不是运行时交互模式 |
| `posters` | 非空数组、ID 唯一 | 默认创建一张；数组不封死未来扩展，但当前不提供多海报 UI |

每个 Poster 只允许以下字段：

| 字段组 | 字段 | 约束/所有权 |
| --- | --- | --- |
| 身份与文案 | `id`, `title`, `subtitle` | ID/标题非空；subtitle 为字符串 |
| 显示与锚点 | `monitorIdentity`, `anchor`, `normalizedRect` | 逻辑业务位置；物理坐标和 DPI 归宿主 |
| 尺寸与呈现 | `width`, `height`, `collapsedState` | 正整数逻辑尺寸；`expanded/collapsed` |
| 自动收起 | `autoCollapseEnabled`, `autoCollapseDelaySeconds` | boolean；延时 `1..300` 秒 |
| 锁定 | `isLocked` | 只持久业务锁定，不持久权限计算结果 |
| 布局 | `layoutKind`, `layoutSpec` | `grid/free`；layoutSpec 为有限、无环 JSON object |
| 模板 | `templateKey` | 非空；不得等于内部 `minimal-fallback` |

`normalizedRect` 只允许 `x/y`，两者均为有限的 `0..1` 数值。`anchor` 只允许 `top-left/top-right/bottom-left/bottom-right/free`。

公开数组 `POSTER_SNAPSHOT_FIELDS`、`POSTER_PERSISTENT_FIELDS`、`POSTER_RUNTIME_ONLY_FIELDS` 是 C-PSS 的机器可读字段边界；C-PSS 不应复制一份私有白名单。

## 4. 禁止持久化

以下字段和派生结果不得进入 Poster 表或快照 JSON：

- `interaction` / `interactionMode`；
- `runtime` / `runtimeMode`；
- PAUSED/STOPPED 原因、可见性、宿主 readiness、FPS 与性能采样；
- 物理窗口坐标、HWND、WorkerW/Progman、device DPI 或宿主命中区域；
- 权限矩阵计算结果；
- 临时 `minimal-fallback`、fallback 失败原因或本次渲染选择。

模板解析结果固定 `persistenceMutation=none`。C-PSS 只能保存用户请求的合法 `templateKey`，不能因加载失败重写它。

## 5. 默认创建输入

C-PSS 在“无正式数据且无可迁移 legacy 数据”时调用正式默认创建：

- 调用方必须提供非空标题和宿主确认的主显示器工作区；
- 只创建 `poster-primary` 一张海报；
- revision 为 `0`；
- `primary/top-right/expanded/unlocked/autoCollapseEnabled=false`；
- 默认模板为 `transparent-default`；
- 几何由 C-PCB-02 算法产生，不能从 A0 `490×392` 或窗口配置硬编码复制。

## 6. 仓库接口与失败边界

C-PSS 仓库至少需要表达以下结果，而不是只返回 null/boolean：

1. `load`：无数据、合法 v1、未知版本、schema/内容损坏、I/O/数据库失败必须可区分。
2. `replace` 或事务 mutation：写入前执行 Rust v1 校验；成功原子替换，失败保留旧数据。
3. `createDefault`：只在明确无数据且 legacy 处理完成后执行；不得用默认值掩盖损坏或迁移失败。
4. `migrateLegacy`：产生来源、adapter migrations、结果 revision 和失败原因；重复执行必须幂等。
5. `backup/retain`：schema 迁移或 legacy 迁移失败时保留可诊断原始数据，不把损坏输入覆盖为空白默认。

SQLite schema 自身必须有独立版本。`contractVersion=1` 不能代替数据库 schema version。

## 7. 唯一 legacy 边界

- 无 `contractVersion` 且包含 `posters` 数组的对象才属于 `LegacyA0`。
- 只要对象含 `contractVersion`，就必须走正式 v1 校验；失败后不得退回 legacy 数组判断。
- 前端只有 `poster-legacy-bridge.js` 可以访问 `desktop-up.a0-demo.v1`、`load_poster_snapshot` 和 `save_poster_snapshot`。
- 正式迁移只通过 `adaptLegacyPosterSnapshot`，并要求真实工作区证据；C-PSS 不读取 `theme/accent/opacity/density/x/y/collapsed/locked` 作为自身 schema。
- 当前 A0 两海报演示数据不是多海报产品能力。C-PSS 必须在迁移夹具中明确处理额外演示项，不能据此创建多海报 UI 或宣称多海报通过。
- C-PSS 完成幂等迁移、失败保留和 Release 回归前，不能删除过渡 JSON 或 localStorage 读取能力。

## 8. 宿主与事件边界

C-PSS 只管理业务持久状态，不直接调用 Windows 宿主。前端投影仍只通过 canonical `sync_host_surface` 提交；不得重新引入 `resize_poster_surface`、`set_poster_interaction_region` 或 `runtime-pause-state` 前端依赖。

## 9. C-PSS 必须自行冻结的事项

以下内容尚未由 C-PCB 决定，C-PSS-01 必须先形成可测试决策再写库：

- SQLite 表拆分、索引、外键和 JSON 列策略；
- 成功 mutation 如何推进 revision，以及是否使用 expected-revision/CAS；
- A0 额外演示海报是丢弃、保留为隔离备份还是迁移为不可见记录；
- 数据库路径、备份命名、schema 升级失败后的回滚/保留策略；
- 过渡 JSON 与 localStorage 的最终退出版本和清理时机。

这些未决项不允许反向修改 C-PCB v1 字段、模板 fallback 或宿主接口边界。

## 10. 验收输入

C-PSS 应复用以下现有证据，并新增数据库事务/迁移测试：

- `tests/fixtures/poster-contract-v1.json`；
- `tests/fixtures/poster-geometry-v1.json`；
- `tests/fixtures/poster-permissions-v1.json`；
- `tests/fixtures/poster-template-resolution-v1.json`；
- `tests/poster-contract-seal.test.mjs`；
- Rust `poster_contract`、`poster_snapshot` 与 `poster_template_resolution` 测试。

C-PSS 完成门还必须包含 schema 向前迁移、重复迁移、事务失败保留、损坏输入不重置、旧数据备份和当前单海报 Release 核心回归。
