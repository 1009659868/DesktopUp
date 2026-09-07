# V3-04-M02：目标化命令、三端投影与实体读集 CAS

日期：2026-09-07
状态：AUTO PASS；147/147、Release、隔离实际进程 smoke 通过；受影响单海报人工交互回归待完成。不是多窗口完成声明。
前置：[M01 数据合同](POSTER_CATALOG_SCHEMA_V3.md)、[通用多海报设计](../../20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md)。

## 1. 当前交付和边界

替换生产命令链的全局版本冲突及单 Poster 投影；沿用同一个 SQLite 数据库、全局提交序列、领域几何策略和 DesktopHost。新增/复制/删除/批量命令有合同及自动证据，尚无新增按钮、删除确认弹窗或多窗口 registry。

实际落点是现有 StateProjectionCoordinator + StateCommandPolicy + IStateCommandRepository/SqliteStateCommandRepository，不另建一套状态权威；架构目录里的 CatalogApplicationService/ScopedCommandCoordinator 是职责名称，不要求生成同名类。

## 2. 三类版本

| 版本 | 含义 | 推进条件 |
| --- | --- | --- |
| global Revision | 全数据库一致快照/提交序列 | 一个成功有变更事务推进一次，包括多目标批量；NoChange 不推进 |
| Poster EntityVersion | 指定 PosterId 的行版本 | 此海报被修改才推进；与其他海报/Settings 无关 |
| Settings EntityVersion | 全局 Settings 行版本 | Settings 被修改才推进 |
| CatalogVersion | 目录成员/内部层序版本 | 新增、移除、次序变化；不随普通拖缩、锁定或 Settings 更新推进 |

PosterRevision/SettingsRevision 仍保留“该行最后一次写入对应的全局提交序列”，不能与 EntityVersion 互换。

新命令 ExpectedRevision 表示客户端观察到的全局序列，用于诊断和防止未来 revision；真正的并发条件是 ReadSet。旧 Poster/Settings 命令若 ReadSet=null，明确保留 global-CAS 兼容语义；当前 App、海报与托盘生产调用均显式传读集。

## 3. 目标、读集和入口权限

| 命令 | 明确目标及必要读集 | 入口/操作 |
| --- | --- | --- |
| PosterStateCommand | PosterId + 该 EntityVersion | Move、Resize、SetLocked、SetCollapsed、SetVisible、Rename；只允许改变该命令所属字段 |
| SettingsStateCommand | SettingsVersion | 暂停、性能模式、自启意愿字段；不直接实施开机自启 |
| CreatePosterCommand | CatalogVersion | 中心/托盘；核心生成 PosterId 和独立 ThemeInstanceId，追加内部顺序 |
| ClonePosterCommand | SourcePosterId 的版本 + CatalogVersion | 中心/托盘；仅复制配置，新身份/主题实例，CollectionId=null，不复制实际文件 |
| RemovePosterCommand | 确认目标 ID/实体版本 + CatalogVersion + Confirmed | 中心/海报菜单；没有确认不能提交；不提供托盘直接删除 |
| BatchPosterCommand | 显式、唯一的 PosterIds + 每个实体版本 | 中心/托盘；纯锁定或显隐；全部校验后一次提交 |
| Batch AllPosters=true | 上述读集 + CatalogVersion，目标集合必须等于目录 | 防止点击“全部”时遗漏并发新建海报；隐藏海报也属于集合 |

调用者可声明额外读取依赖，事务必须检查全部依赖，不能因为某实体未写入就忽略它。若一个批次含多个过期/缺失目标，Conflicts 返回每个失败的 kind/id/expected/actual/reason，整批不写入。含缺失目标时总状态 NotFound；其他读集不匹配为 Conflict。

未锁定/收起几何限制和收起时保持顶部锚点逻辑沿用已验证 V3-04-03。参数、Item、主题载荷、内部层序管理等后续命令不在本项提前实现。

## 4. 事务与幂等

处理顺序：冻结输入集合 → 校验请求 → 指纹 → SQLite immediate transaction → 读取一致目录/Settings → 回执匹配 → 校验全读集 → 领域策略/写集覆盖校验 → 写目标行 → 推进一次 global revision → 写回执 → 读取完整新快照 → commit → 发布。

- 指纹包含协议版本、命令类型、来源、目标、参数、观察 revision 和读集；读集字典、批量 ID 规范排序。相同 requestId 不同内容明确拒绝。
- state_command_receipts 为持久回执。成功有变更记录 CommittedRevision；NoChange 也保存请求指纹，但不推进业务 revision，也不伪造提交序号。
- 跨重启重复同一命令返回 Replayed，携带原 CommittedRevision/ResultPosterId 和当前一致快照；原目标已删除仍不重新创建。
- 冲突/取消/验证失败不保留成功回执；用户重新确认后可以提交，但不得冒用已有成功请求 ID。
- 新旧仓储协议共享请求 ID 命名空间防护：不能把新协议 NoChange ID 交给旧 global-CAS 入口再次执行。
- 中途 SQL/触发器失败回滚全部目标、head、operation 和回执。失败时投影保留最近成功快照，不拼接“新 Poster + 旧 Settings”的猜测视图。

## 5. 删除影响预览

PrepareRemoval(PosterId) 先读当前权威快照，返回名称、目标 ID、实体/目录版本、IsLastPoster、CollectionId 与后果说明。确认命令携带这个快照的读集；不能关闭弹窗后重新读取新版本再静默提交。

Confirmed=false 不修改状态。确认期间同目标变化或成员改变会拒绝旧确认。删除只解除海报展示配置，不调用文件系统；实际文件、受管集合后续恢复入口的责任仍按 V3-05 的 journal/所有权合同，不借本项实现文件删除。实际后果确认 UI 分别在 M04、V3-04-04 接入。

## 6. 初始化与零海报

- 未初始化：同一 immediate transaction 只创建一张默认海报。多连接同时初始化仍只有一张。
- 已初始化 0 张：返回有效空列表，不异常恢复、不补造默认；Settings/中心/托盘投影有效。
- 已初始化 N 张：完整读取既有集合，即使不存在 poster-main 也不额外创建默认。
- 删除至零后再新增：新身份、EntityVersion=1；不能复用已删除身份。

单海报壳已补空目录不创建窗口、中心空态、托盘禁用无目标操作和海报按自己的 ID 消费投影；真正 0..N 会话创建/销毁、显隐物理同步与恢复编排仍在 M03，本项不能通过调用数据命令假称桌面上已出现 N 张。

## 7. 三端投影与发布

- DesktopUpProjection/ControlCenterProjection 同时提供只读 Posters 列表（StackOrder、PosterId 排序）与 PostersById。
- 每一海报、Settings、中心和托盘投影具有同一个 global Revision；局部行版本单独保留。
- 旧 Poster 属性为可空的过渡选择别名：优先 poster-main，否则目录第一张；它不是命令路由身份。真实海报窗口按已绑定 PosterId 查字典，不随别名变化切换成另一张。
- TrayProjection 包含总数、可见数、锁定数和 Empty/None/Some/All；空集合不算“全部已锁定”。现有托盘“锁定全部”已改用 AllPosters 批量读集命令。
- StateProjectionChangedEventArgs 增加 ChangedPosterIds（包括移除 ID）、SettingsChanged、CatalogChanged。其他海报或纯 Settings 变化不重建当前海报物理帧。
- 一个协调器内投影按 revision FIFO 发布，允许订阅者重入命令；坏订阅者异常被隔离，不把已提交事务改判失败，不阻断其余端。
- 多进程写入串行性由 SQLite 保证；另一个协调器写入后可通过 Refresh/下一命令刷新，尚未引入跨进程实时推送。

## 8. schema v4 与兼容

只追加 migration v4：app_settings.entity_version 和 state_command_receipts；v1、v2、v3 SQL/校验码保持原样。v3 指纹从已验证 M01 程序集提取后固定在 LegacyStateDatabase 夹具。

升级前保留并按源版本验证备份；新增表/列在迁移提交前读回验证。错误同名表、缺失当前表或 Settings 版本会失败保留；不得仅写 user_version 宣称迁移成功。正常用户库没有在本轮自动迁移。

## 9. 验证、产物与后续

147/147 合同测试、完整 WinUI Release 0 warning / 0 error。覆盖读集并发、批量故障、持久重放、旧接口兼容、0/1/3 数据集合、删除预览与 source 校验、发布重入、v3→v4 源备份及故障保留。精确产物、哈希与未执行门见 [VERIFICATION](VERIFICATION.md)。

实际进程 smoke 已改为强制传 DataRoot，并仅通过子进程 DESKTOPUP_V3_TEST_DATA_ROOT 指定独立测试库；普通启动仍使用原用户库。脚本在已有 DesktopUp 运行时拒绝启动，避免单实例消息误入旧版。新增测试参数不是便携配置格式或配置导入功能。

旧版退出后，隔离 smoke 已通过（PID 40212，1 中心/1 海报、二次激活复用、退出资源0）。当前仅待锁定/收起/拖缩/中心重开及基本桌面层级人工回归。确认后下一制作项是 M03：PosterSessionRegistry 与共享 Shell/显示恢复；不进入 Item/主题/AI 重构，也不改已验证 Host 算法。
