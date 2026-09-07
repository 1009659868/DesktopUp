# V3-04-M01：多海报实体、兼容迁移与目录仓储合同

后续实现说明：本文记录 M01 时点与保留的目录兼容仓储。M02 已追加 schema v4、目标化命令/实体读集 CAS 与三端集合投影，实际合同见 [TARGETED_COMMAND_PROJECTION_V2](TARGETED_COMMAND_PROJECTION_V2.md)。下文“下一步 M02”与 global-CAS 限制是历史边界，不是当前生产命令实现。

日期：2026-09-07

状态：PASS（数据/仓储门；不代表多窗口人工门通过）

前置：V3-04-03 用户人工 PASS、[通用多海报设计合同](../../20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)。

需求对应：通用多海报工作流 RP-MP-01/02 的实体、迁移和目录子集。

## 1. 本项交付与边界

复用已有 Domain → Application → Infrastructure 分层及唯一 SqliteStateStore。新增目录仓储，不另建配置文件、数据库或第二套状态权威。不修改已验证的 DesktopLayerAttachment、输入岛和 WinUI 视觉实现。

M01 交付的是多海报配置可持久化、可列出/创建/复制配置/移除的底座。运行端仍是单海报；命令投影的目标化和实体读集 CAS 在 M02，真实多实例 registry 在 M03。当前没有新建海报 UI，也没有把六张演示 Item 写入正式库。

本项没有移植新的 DeskBox 代码、增加第三方包或改变许可证。借鉴边界继续遵循现行架构和既有来源账本；这里是 DesktopUp 自己的状态安全补强。

## 2. 实体合同

PosterDefinition 保留全部 v2 构造字段，追加 init 属性：

| 字段 | 合同 |
| --- | --- |
| PosterId | 原有稳定身份；旧 poster-main 原样保留；目录 Create/Clone 由核心生成 poster-UUID |
| Purpose | 非空用途标签，默认 general；游戏、工作等不再限制为唯一用途 |
| IsVisible | 用户持久显隐意图，默认 true；不是本轮运行窗口的即时可见性报告 |
| StackOrder | 非负整数，仅海报内部相对顺序，不代表 Win32 topmost；排序为 stack_order、poster_id |
| CollectionId | 可空、非空时最长 128 的预留关联；允许多海报共享；尚无 Collection/ContentItem 表 |
| ThemeInstanceId | 每海报独立配置身份，数据库唯一；并非完整 ThemeInstance 表或已加载主题 |
| EntityVersion | 在 PosterStateSnapshot 中，初始 1；每次有效 Save 加 1；不能由 UI 自行覆盖 |

旧几何、屏幕归属、锁定、收起、自动收起、layout kind、layout JSON、template key、标题等完全保留。旧 Save 会保存新增字段、更新 EntityVersion；现有拖缩使用 seed with，只修改几何，不丢失这些字段。

ThemeInstanceId 的迁移值是 theme-加旧 PosterId；Create/Clone 使用新 UUID。Clone 保留源配置、CollectionId、可见/锁定/收起等意图，但得到新 PosterId、新 ThemeInstanceId 和追加层序。模板键可相同，关联内容不复制。Create 忽略调用者配置中的 PosterId、ThemeInstanceId、StackOrder，避免外部决定身份。

集合/主题关联当前只是可持久化的身份合同，不证明引用存在，不加载文件、不运行代码、不建立伪内容行。正式集合有效性/外键/删除关系在 V3-05，完整主题在 V3-07；后续继续追加 migration。

## 3. schema v3 与兼容性

迁移名：multi-poster-catalog。追加 posters 的 purpose、is_visible、stack_order、entity_version、collection_id、theme_instance_id，以及层序索引/主题实例唯一索引。

新增：

- poster_profile：singleton_id=1、profile_initialized、catalog_version、state_revision。
- poster_catalog_operations：operation_id、request_sha256、result_poster_id；operation_id 以延迟外键关联已有 state_operations。回执与业务 mutation、全局日志在同一事务提交。

保留 v1/v2 SQL 与 checksum；.gitattributes 仅为该迁移源文件固定 LF，防止 Windows Git checkout 改变原始字符串指纹。独立基线取自此前验证过的 out/verify/v3-04-03/app-final 程序集，测试中固定：

- v1：AE9E436DEB9280A4DF7864A541EDC08A8964005ECCD19B47FCF47767FBFD3422
- v2：75FFE962DA1729676D8183332FF8B3EFC58BE29E8D71DAC3F1F4E71FCED887C2

迁移只更新 schema 版本，不伪造用户业务提交、不改变 global revision 或旧 PosterRevision。旧海报 EntityVersion 从 1 开始；既有多行按 PosterId 确定初始层序。已有海报时 profileInitialized=true、catalogVersion=1；无海报的 v1/v2 源库仍是未初始化，允许一次默认创建。

迁移前先生成并按源 schema 验证备份。M01 增加了实际目录表结构、profile 行和外键读回校验：v3 DDL 执行后、写账本/提交前检查；正常打开与 v3 备份验证也检查。不仅依赖 user_version、迁移账本或 SQL 批次返回值。

验证中注入同名错误表，曾观察到批量迁移返回后账本更新但错误表仍存在。现由提交前结构校验拒绝，整个 v3 事务回滚；保留 v2 原库、旧字段及源库备份。未对数据库驱动的内部原因作未经验证的归因。

新库不可由旧 schema v2 程序直接打开；回退需使用迁移前备份并按后续恢复流程操作，不手工降低 user_version。

## 4. 首次初始化与零海报

List 不写库；新库是 profileInitialized=false、零海报、global revision=0、catalogVersion=0。

兼容入口 GetOrCreateDefault：

1. 在同一个 catalog 读快照中读取现有海报、初始化标志和 global revision。
2. 目标已存在则原样返回，不覆盖旧配置。
3. 目标缺失但 profileInitialized=true 则拒绝重建。
4. 未初始化时在 CAS 事务内插入默认海报并设置标志；并发者冲突后重读。

Create 同样完成首次初始化；InitializeEmptyProfile 用于明确选择零海报的首次状态，重复相同请求可重放，不提供重置标志接口。

2026-09-07 后续用户明确：普通首次无配置必须只建一张默认海报。上面的 InitializeEmptyProfile 是底层能力，仅用于明确空方案导入/夹具，不作为普通首启默认。便携配置解析、跨机映射和新增/删除 UI 不是 M01 的已实现内容，继续按 M02..07、04-04..06 接入。

移除最后一张后仍保持 initialized=true。重启不造默认海报；迟到的旧 Create/Clone 请求重放只返回旧回执，不复活记录。旧非空 GetOrCreateDefault 接口在“已初始化且缺失默认”时抛出明确异常，**不是最终零海报运行 UX**；M02/M03 应从 catalog 驱动 0..N 会话，而不是捕获异常后再次造默认。

## 5. 仓储和版本语义

IPosterCatalogRepository：

| 操作 | 输入/输出和失败 |
| --- | --- |
| List | 同一个只读事务返回 global Revision、CatalogVersion、ProfileInitialized、按层序排序的只读海报快照列表；所有条目 Revision 相同 |
| Create | 配置＋expected global revision＋operationId；校验后分配新身份，追加层序 |
| CloneConfiguration | sourcePosterId＋版本/operationId；在写事务中读取源配置；源缺失 NotFound |
| Remove | posterId＋版本/operationId；只删海报配置，缺失 NotFound，不删原文件/集合/主题包 |
| InitializeEmptyProfile | 首次零海报初始化；已初始化的新请求 ValidationFailed |

M01 仍使用已有 **global CAS**，不同海报并发也可能 Conflict；没有宣称实体读集 CAS 已完成。M02 才改变命令校验口径，使无关实体写入不造成伪冲突。

- global revision：每笔成功新事务加 1，覆盖状态与操作回执。
- PosterRevision：该海报最后一次写入的 global revision，保留旧投影语义。
- EntityVersion：仅该海报有效更新时加 1；设置或其他海报更新不推进。
- CatalogVersion：成员新增/删除或内部层序改变时加 1；几何、显隐、标题、设置等仍反映于 global revision 和对应实体版本。它不是全部目录显示数据的 revision。
- 零初始化推进 global revision，但不改变空集合的 CatalogVersion。

状态结果包括 Committed、Replayed、Conflict、NotFound、ValidationFailed、Failed。失败不推进 revision、不留下半行 profile/海报/操作日志。写入结果的 CommittedRevision 指原始提交；Snapshot/CurrentRevision 是随后同一读事务取得的最新目录，可能比原提交更新。不能把最新快照冒充原提交的历史视图。

Create/Clone/Remove 的 operationId 回执跨进程重启保留。相同 ID/原 expected revision/同一请求重试仅重放；同 ID 不同目标、类型或有效配置拒绝；不能用新 ID 盲重试结果不确定的写入。删除配置不删操作回执，所以重放仍能返回原身份而不创建新实体。

## 6. 验证与后续

真实 SQLite 回归包括：0/1/3 配置、独立身份/关联、旧默认入口、并发首次启动、删至零重启、创建和克隆重放、移除后晚到回执、双连接冲突、实体/目录/全局版本、无效输入、缺失目标、插入/移除后回执故障回滚、v1/v2 指纹冻结、带全部旧字段的 v2 升级和源备份、迁移中途失败、损坏 profile 失败保留。既有损坏数据库/篡改迁移账本用例继续运行。

完整测试结果和构建证据见 [VERIFICATION](VERIFICATION.md)。没有启动用户真实数据库进行迁移，没有移动桌面文件，没有新增视觉人工验证。本数据门无新增鼠标操作验收要求；不能据此关闭 M03..07。

下一步：V3-04-M02，目标化 command/query、PostersById/列表/Tray 汇总投影、实体读集 CAS 和批量纯状态事务。保留已验证宿主/拖缩实现。
