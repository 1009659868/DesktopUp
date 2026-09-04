# V3-04 Poster/Settings repository 合同

状态：`FROZEN FOR V3-04-02 — SCHEMA VERSION 2`

最后更新：2026-09-04

## 1. 目标

V3-04-02 把海报状态和应用设置纳入 V3-04-01 已验证的同一 SQLite 状态权威。海报、设置和后续 projection 共享一个全局 committed revision；窗口、控制中心和托盘不得各自持有可独立提交的第二份真相。

本项追加不可变 migration v2，不修改 migration v1 的历史文本或校验值。

## 2. Poster 持久字段

`posters` 行保存：

- 稳定 `poster_id`；当前内建主海报为 `poster-main`；
- 标题、子标题、布局类型、布局参数 JSON 和模板 key；
- 显示器稳定设备名；
- 相对工作区的 DIP 左上角 fallback；
- DIP 宽度和展开高度；
- 水平/垂直锚点；
- 归一化宽度和展开高度；
- 锁定、收起和自动收起配置；
- 最后修改该行的 `state_revision` 及时间戳。

以下状态明确不持久化：HWND、物理像素矩形、DPI、输入区域、桌面 generation、编辑模式、滚动瞬时偏移、动态效果的运行时暂停原因。显示拓扑变化只重投影当前已提交逻辑几何，不产生隐式数据库写入。

## 3. Settings 持久字段

`app_settings` 是单例行，保存：

- 用户手动暂停动态效果；
- 性能模式：`automatic`、`static` 或 `30fps`；
- 用户期望的开机启动选项。

开机启动这里只保存用户意图；真正写入 Windows 启动入口属于 V3-09。临时的省电、全屏或故障降级原因不得覆盖用户设置。

## 4. revision 与写入合同

1. Poster 与 Settings repository 的每次业务写入都必须走 `IStateStore.ExecuteCompareAndSwap`；
2. 调用方携带读取快照的 expected global revision；
3. mutation、全局 revision 增长和 operation journal 在同一 SQLite transaction 中提交；
4. repository 返回 `Committed`、`Conflict`、`Replayed`、`ValidationFailed` 或 `Failed`，并尽可能附带最新一致快照；
5. 每行的 `state_revision` 表示该行最后一次修改发生在哪个 global revision；
6. 若 global revision 仅被 Settings 推进而 Poster 行 revision 未变，海报应用服务允许安全重基一次；若 Poster 行已变化，则不得覆盖并发结果，运行时恢复 repository 返回的最新快照。

## 5. 运行时接入

- 应用在创建托盘、控制中心和海报前初始化 StateStore 并读取 Settings；失败则中止启动，不生成第二份默认状态；
- 桌面宿主和显示拓扑稳定后，海报读取 `poster-main`；首次运行才以默认几何创建该行；
- 拖动/缩放预览不写数据库；手势完成只提交一次；
- 锁定和收起先准备本地帧与输入布局，再提交同一个 Poster snapshot；失败或 CAS 冲突时恢复最新 committed snapshot；
- 启动恢复采用逻辑 DIP 尺寸和工作区锚点投影；当前多显示器选择仍以主屏 fallback 为准，更完整的显示器身份选择不在本项宣称范围内。

## 6. 边界

V3-04-02 没有实现：

- 控制中心、托盘和海报的实时 command/query projection；
- 用户可见的冲突处理 UI；
- 最近可用状态选择和数据库恢复界面；
- 真实 ContentItem、启动统计、热度排序或动态图标缩放；
- Windows 开机启动注册。

这些能力分别属于 V3-04-03 至 V3-04-08、V3-05、V3-06 和 V3-09。

## 7. 自动证据

- schema v1 → v2、默认 Settings 和空 Poster；
- 默认 Poster 创建并推进 global revision；
- 几何、锁定、收起跨 repository/连接重建恢复；
- Poster 写入 CAS 冲突和幂等重放；
- Settings 与 Poster 共享 global revision；
- 非法 Poster 不推进 revision；
- Release 合同测试 `87/87 PASS`；
- DesktopUp.App Release 构建 `0 warning / 0 error`。
