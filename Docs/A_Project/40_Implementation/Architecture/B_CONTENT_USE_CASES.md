# 架构整理 B：真实内容打开用例

日期：2026-10-16；继续 `impl/architecture-foundation`，不自动提交/推送/合并。前置[A批](A_FOUNDATION.md)集中人工已由用户确认通过。

## 1. 目标与实施边界

从真实打开/定位切片抽出应用服务，让打开成功事实的生成不再依赖卡片视图。现有 `ReferenceContentActions` 同时负责请求、Worker、账本和视图观察；抽取后只将请求交给服务、将带来源见证的结果交给原视图。

- `ContentActionService`：当前关系读取、打开准入/去重、派发前见证复查、Worker返回校验、原子完成记录；定位不计打开事实。只依赖现有Worker/账本端口和当前目录读取，不持有视图、窗口或SQLite实现。
- `ReferenceContentActions`：保留原UI动作接口，等待服务结果并调用原 `Observe(expected)`；刷新仍只属于视图，不进入打开服务。
- `App/Bootstrap`：按原启动位置和顺序创建一个应用级服务，空间运行器只保留服务端口，不持有打开账本。Worker仍由App唯一管理、退出仍先StopAsync，再等待在途内容操作；不借抽取更改该次序。
- `IContentOpenJournal`：独立端口文件，SQLite实现/schema/事务不改；删除经全库确认未使用的旧DEMO dispatcher空端口，修正过时注释。

不做热度统计/策略、拖放/重命名/删除事务迁移、第二份账本、统一事件总线、目录大搬迁、新交互或额外确认框。本批是结构/依赖抽取，不冒充A批字节不变的纯移动。

## 2. 连续实施与等价边界

先记录A人工与本计划 → 抽服务/结果和薄适配器 → 组合根接线 → 定向检查和产品候选；内部步骤不单独人工。

保持：取消前不派发；相同ActivationId不重复打开；准入后关系变化不打开；无效/失败结果不产生成功事实；已确认交接即使海报移除仍记录事实，旧来源结果不污染新卡片；定位不记热度事实；账本提交失败不自动重试外部操作。沿用既有异常/取消结果映射，不借抽取改变语义。

## 3. 精简验证

影响面仅真实打开/定位的依赖与组装。复用 `RealContentUsageTests` 中相关打开事实/动作测试及 `RealContentOpenTests`；补少量无视图服务执行和适配器陈旧观察防护。结构检查阻止执行服务重新依赖视图/SQLite，正常产品构建检查接线，必要时一次隔离启动退出。

不跑累计全量、全部拖放/安装卸载矩阵，不重复A模板/布局人工。若需集中人工，仅在独立候选对一个自建样例打开并定位，确认行为不变；无用户结论不记人工PASS。所有样例在项目out，不动真实文件/桌面/开机设置。

## 4. 实施结果

**USER PASS / B批CLOSED**。2026-10-16用户确认“验证通过，符合预期”，覆盖下述打开/定位集中人工。A/B均不重开；继续原功能分支推进[C批热度运行边界](C_HEAT_RUNTIME.md)，未提交/推送/合并。

- 应用执行入口：src/DesktopUp.Application/Features/Content/Open/ContentActionService.cs，打开/定位通过IContentActionService返回反馈与来源观察结果。不会调用视图、窗口或数据库实现。
- 适配器：同目录ReferenceContentActions.cs仅等待结果并调用原Observe(expected)，刷新留在视图。ReferenceContentRuntime.cs不再混放执行类/账本端口；其自身缓存/增量观察代码未改。
- 组装：src/DesktopUp.App/Bootstrap/App.ContentServices.cs按原启动时机先收尾未完成请求，再建立原Worker与服务；全应用一个执行服务。PosterSpatialRuntime只保留服务端口，打开账本不再进入空间运行器。
- 依赖：IContentOpenJournal移至Ports/Content独立文件，增加IContentActionService；删除全库无实现/无调用的旧IContentOpenDispatcher空接口。打开合同只修正过时注释，SQLite实现/事务/schema/Worker协议、交互与退出顺序未变。
- 不是全App解耦完成：文件搬移/归还等用例仍有具体存储耦合，按后续实际影响逐步处理。没有新增统计消费者、事件总线或第二份事实库。

### 定向验证与边界

| 检查 | 结果 |
| --- | --- |
| 合同/用例 | 50通过：7个服务边界场景、22个原打开合同、12个原打开事实/动作场景、9个依赖边界；排除本批未影响的Detach、布局、图标等其他测试。原打开/定位场景同时运行无视图服务与视图适配器版本 |
| 关键反例 | 取消前不占请求；准入后关系变化不派发；外来回包/Worker异常不计成功不重试；在途移除/修复后仍记确认交接、丢弃旧来源观察；事实提交失败抛回且不重复外部执行 |
| 产品构建 | scripts/build.ps1 -Offline -OutputDirectory out/architecture-content/product02：0警告/0错误；初次product01发现组合根nullable警告，最终版本用本地已初始化服务引用消除，未改变业务行为 |
| 编译边界 | scripts/check-architecture.ps1 -Offline：产品99源/0验收源；Review配置164源/65验收源；生产集合一致，Preview+Review互斥通过。本批不再跑A的历史-VerifyMoveBytes，也未重复构建整套验收包 |
| 组装/退出 | product02隔离profile启动中心/4海报后正常退出：PID33084，exitCode=0，0 error/critical，activeResources=0。工具上下文高权限，非普通权限原生人工证明 |
| 入口 | 新start-content-service-review.ps1在Windows PowerShell 5.1实际Prepare/Open通过；旧A入口/包/配置不改 |

证据：out/architecture-content/tests/content-service.trx、product-build-final.log；应用日志在out/drop-d3/architecture-content-review01/profile/logs/desktopup.jsonl。定向过滤为：

```text
(FullyQualifiedName~ContentActionServiceTests|FullyQualifiedName~RealContentOpenTests|FullyQualifiedName~RealContentUsageTests|FullyQualifiedName~CoreProjectsDoNotReference|FullyQualifiedName~ProductionProjectReferences|FullyQualifiedName~ContentExecutionDoesNotOwn)&FullyQualifiedName!~RealContentUsageTests.Detach&FullyQualifiedName!~TailPinsStayLegal&FullyQualifiedName!~RealViewLoads&FullyQualifiedName!~MissingViewRetains&FullyQualifiedName!~UntrustedRasterShape
```

最终DesktopUp.dll SHA256：9B0BF864E3B580252F5541218304F5BE5F72FCD74A4C7F16DFD89E23AD7726D3；
DesktopUp.Application.dll：18EAD6326BCAD481D00FF54320350DC7672AB09CC4E99480B4123DA576ACAEAA。
入口同时核对两者，避免只更新服务DLL后沿用旧验收身份。

### 一次集中人工

产品候选已打开，PID21900（仅当次身份）。入口：[start-content-service-review.ps1](../../../../scripts/start-content-service-review.ps1)，支持Open/Prepare/Status。数据和样例全在out/drop-d3/architecture-content-review01，复用旧四海报夹具格式，但创建独立数据；没有桌面样例、安装或自启登记。自动进程已退出，当前只留人工候选。

**只验一个体验点**：在“A · 普通收纳”双击“A-文件夹”，应正常用资源管理器打开；再右键该卡片“在资源管理器中定位”，应定位同一个文件夹。文件/卡片不应消失、变名或重复。其他B/C/D海报无需重跑拖放/映射矩阵。退出后本批样例仍保存在隔离out，用户真实内容和A批验收数据不动。

用户已确认本批通过；不扩大为自启真实登录或其他既有未验通过。C批开工未发现DesktopUp运行进程，旧包/配置保留于隔离out；本批没有需清理的桌面样例。下一步C仅收敛热度直接需要的海报运行边界，再进入V3-06，不以所有架构阶段完成阻塞主线。
