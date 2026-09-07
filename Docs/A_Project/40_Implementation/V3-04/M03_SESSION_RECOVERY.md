# V3-04-M03 会话注册与共享恢复

状态：AUTO PASS / MANUAL PENDING（2026-09-07）。用户同意从 M02 进入 M03；不补写未逐项回报的 M02 人工 PASS。

实现边界：目录投影 → PosterSessionRegistry → 按 PosterId/epoch 绑定的会话；Shell/显示发现、generation gate、节流和环境监听只共享一份。零海报仍保留控制中心、托盘和环境监听。沿用已通过的 DesktopLayerAttachment、输入岛、拖缩和白底防护，不重写这些算法。

初始化只在 bootstrap 执行一次：未初始化目录原子建立一个默认；已有目录包括空目录原样恢复。隐藏或删除释放对应 HWND、输入岛和回调；重建必须读取当前目录，旧 epoch/已取消的回调不能复活窗口。局部创建/恢复失败标为 degraded，不修改持久状态、不阻止其他会话；重试逐实例读回。

验证结果：163/163 合同测试（保留全部旧宿主/状态测试，新增16项）；Release 0 warning / 0 error。最终产物隔离启动验证首次1张、已有3张、已初始化0张，均重复激活同一控制中心、一个共享监听器、逐实例generation读回和退出资源0；另验证恢复等待中退出会取消任务、无迟到窗口。多窗口重叠输入/内部 stack order 为 M04，完整参数化端到端矩阵为 M06。

## 1. 实际模块与运行边界

| 设计职责 | 实际落点 | 约束 |
| --- | --- | --- |
| A07 会话注册 | Application/PosterSessionRegistry | UI dispatcher 串行；消费完整 committed catalog，不能写库或生成默认配置 |
| 会话端口/工厂 | IPosterSession、IPosterSessionFactory；App/WinUiPosterSessionFactory | 工厂按 PosterId＋epoch 绑定 visual/input/watchers；没有额外单海报权威 |
| A08 共享环境协调 | App.xaml.cs 中唯一调度与 DesktopRecoveryVersionGate | 一份 locator/topology service、一份健康轮询/节流队列；不是每张复制发现循环，也不要求新建同名类 |
| 环境信号接收 | 隐藏顶级 WinUI anchor＋DesktopEnvironmentWatcher(guardPosterWindow:false) | 从不 Activate/Show，不是海报，不是输入岛；零海报仍能接收 TaskbarCreated/显示广播；message-only HWND 不用于此广播接收 |
| 单实例防护 | WinUiPosterSession 内 watcher | 每张保留已经验证的背景擦除、MA_NOACTIVATE、HTTRANSPARENT、bottom reassert 与 z-order 约束；向共享调度转发环境信号 |
| 资源与证据 | PosterSessionLedger / PosterSessionLog | 资源键为 poster/编码后的ID/epoch/原资源名；日志含 posterId、sessionEpoch 和 detail；句柄按十六进制文本记录 |

本轮沿用 V3-02/03 已吸收的 DeskBox 经验与源码基线；没有重新选择宿主方案，没有复制第三套拖缩或恢复算法。DesktopLayerAttachment 本身已按 HWND 保存附着状态，本轮没有改写它。

## 2. 初始化、恢复与取消

1. App 在第一份稳定桌面/显示快照到达时初始化权威目录一次；仓储原子决定首启默认或恢复既有集合。窗口 Show 不再调用 EnsureInitialized、不读取单 Poster 选择别名。
2. Registry 对可见配置建立会话；隐藏/删除取消 lifetime，再释放输入、watcher、visual、lease。空目录不创建占位默认；中心和托盘独立存活。
3. 共享恢复等待期间暂停新会话创建。提交前校验 request＋Shell/display generation，恢复读取最新目录，不捕获旧 ID 列表。每张独立 Recover/IsAttached/generation readback；一张成功不能代表全部成功。
4. 局部失败标 degraded、取消旧 lifetime、清理半创建资源；下一次以新 epoch 重建。健康会话不会随局部重试重建；局部失败不主动失效共享 Shell locator。真正 Shell 信号仍按原协议失效并重新发现。
5. 清理失败保留可诊断引用与失败状态，后续重试；不会把未清理项当 ready。关闭所有会话时，一张异常不阻断其他会话清理；总账本未清空不能报告零残留。
6. 每张启动/恢复优先匹配保存的显示器名称，缺失才回退主屏；显示变化从 committed DIP 配置重投影，不把临时钳制位置写回库。未做混合 DPI/多屏认证。
7. 退出先让版本门失效并取消共享任务，停止计时器/监听，释放会话，再等待异步恢复结束；最后释放中心、托盘和服务并记录总账本。

## 3. 测试与证据

- 合同：tests/DesktopUp.Contracts.Tests/PosterSessionRegistryTests.cs，16项；使用真实 SQLite/coordinator 驱动目录，session 使用可控假实现覆盖延迟恢复、局部失败、重入删除、取消/旧版本、逐实例清理失败与日志序列化。
- 旧架构夹具更新为“bootstrap 持有 projection coordinator；窗口只消费 projection/command”；未删除旧宿主测试。
- tests/DesktopUp.SessionFixture 只允许全新的隔离目录，生成0..3张配置，不往用户真实配置写测试数据。
- scripts/smoke-control-center.ps1 增加 ExpectedPosterCount、ID去重、共享监听次数与恢复提交检查；仍拒绝并存的 DesktopUp 进程和真实用户目录。
- 详细 PID、路径、hash、初始失败及最终结果见 [VERIFICATION](VERIFICATION.md)。实际 smoke 进程 isElevated=true；清单仍 asInvoker，不能据此宣称普通权限认证通过。

## 4. 短人工门与启动命令

先从托盘正常退出旧 DesktopUp。下面使用本轮已生成的三张隔离配置，不读写日常用户配置：

```powershell
$env:DESKTOPUP_V3_TEST_DATA_ROOT = 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m03\smoke-three'
& 'G:\A_\Tools\DesktopUp_V2\out\verify\v3-04-m03\app\DesktopUp.exe' --show-control-center
```

三张的位置和状态不同：A 常规、B 锁定、C 收起。显示文案仍是旧演示 NIGHT OPS；独立标题/样式与参数渲染在 M05，不把配置中有标题当作 UI 已实现。测试完成后从托盘退出；需要从同一终端日常启动时先执行 `Remove-Item Env:DESKTOPUP_V3_TEST_DATA_ROOT`。

1. 确认三张海报、一中心、一托盘；A拖动不改变B/C的位置，B保持锁定，C展开/收起可用；关闭中心后海报继续工作，双击托盘只复用一个中心。
2. 点击、桌面刷新、Win+D、普通窗口覆盖维持旧行为；无新增白底或顶层问题。
3. 如愿意进行会影响当前桌面的操作，再手工重启 Windows 资源管理器：三张都恢复且不重复；随后普通退出/启动保持配置，退出无残留。

不重跑安装、自启、完整 DPI 矩阵。自动 smoke 并未主动重启 Explorer，也未通过肉眼确认白底/闪烁/层级；这些仍为人工待确认，不自动提升为 PASS。

## 5. 下一步

M03 短人工门完成后进入 V3-04-M04：多 cluster 命中、唯一 capture、内部层序、重叠路由及“...”删除确认。当前托盘编辑入口仍通过中心选择别名作用于一张，不能冒充完整多海报编辑 UI。新增/删除命令在 M02 已有，但管理按钮和确认弹窗仍按 M04/原 V3-04 管理入口实施；本轮不提前实现真实文件收纳、主题/热度/AI或配置导出。
