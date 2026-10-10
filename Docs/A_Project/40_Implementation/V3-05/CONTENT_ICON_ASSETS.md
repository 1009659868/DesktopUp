# V3-05 独立图标资产与安全迁移

状态：**05-05-V1 CODE / AUTO PASS**；2026-10-10。当前卡片的可选图标覆盖已贯通原有存储、命令、缓存和真实海报显示链路。V2 更换图标／预览／恢复默认控件、V3 构造卡片尚未实现，本轮不冒称用户已能上传图标。

需求范围归 DEC-037，任务顺序归[工作流 §6.4](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#64-卡片独立图标与构造入口下一步)。实际工作区 DesktopUp_V2，分支 impl/poster-redesign-V3，起点 19be209。本轮无 Git 提交或推送，不修改安装器或日常配置。

## 1. 所有权与操作合同

共享 ContentItem 继续保存真实目标；PosterItem 的可空 IconAssetId 只覆盖当前海报中的卡片。相同规范化图像可以复用资产，不因资产复用把两张卡片的外观选择绑定。图标不进入布局 JSON，也不将用户选图的绝对路径保存为长期依赖。

操作链：本地图片只读准入 → 辅助进程解码 → 内存中规范化和预览 → 带原卡片关系及版本的确认命令 → 同一 SQLite 事务保存资产和当前卡片引用 → 当前海报缓存换代。V1 提供这条链的底层入口；实际选择器和预览会话在 V2 接入。

- A 换图不改共享目标、参数、工作目录、名称、图钉、跨度或 B。共享来源修复保留卡片图标覆盖。
- 恢复默认提交空引用，重新使用来源提供方图标；不删除原图片或原文件。
- 覆盖图像与来源元数据分开：保留自定义像素，仍经Inspect观察真实来源的失效／恢复和名称，不把自定义图标当文件可用的证据。
- 取消、锁定、关系过期和 CAS 冲突不写资产。已完成的事务不会因随后取消而被伪称撤销；V2 必须丢弃旧预览回调，不替换当前目标。
- 显式图标命令仅允许当前卡片外观修改。最终事务屏障拒绝夹带来源、关系、另一张卡片、布局或设置修改。
- 新建卡片不得借普通添加命令暗带非空资产。V3 需另补图像和关系一起提交的构造合同。

## 2. 规范化与有界解码

首批仅接受扩展名与内容匹配的本地静态 PNG、JPEG、ICO。文件只读打开，复核最终句柄路径；拒绝网络路径、重解析等不合格来源。PNG 动画声明、GIF、SVG、签名不符、截断和超限输入均拒绝，不执行素材代码、不联网、不打开真实目标。

| 层级 | 首批限额 |
| --- | --- |
| 压缩输入 | 每张不超过 8 MiB |
| 源尺寸 | 单轴不超过 4096，单次各帧合计不超过 16 Mi 像素 |
| ICO | 最多 32 个表示帧，选面积最大的有效帧 |
| 规范化输出 | 最长边 256，保持比例，仅缩小不放大；预乘 BGRA，每张最多 256 KiB |
| 自有资产库 | 最多 512 份、有效像素 BLOB 合计 64 MiB；去重及回收后检查 |
| 辅助进程 | 沿用 4 并发、64 个未完成请求、解码 5 秒超时、400000 字符响应上限 |

这些是本轮实现预算，不是用户额外指定的产品参数；64 MiB 不等于含日志、备份、SQLite 页空闲的磁盘总占用。进程隔离限制故障影响并允许取消，不等于操作系统权限沙箱，也不承诺原生解码器峰值内存严格等于像素预算。

ContentWorker 固定使用 Windows 系统 PNG／JPEG／ICO 解码器，输出不保留 EXIF 等源元数据；规范化尺寸和像素再次验证。源图没有高分辨率细节时不制造“高清”。后台缓存只接受有界 BGRA；界面线程不解析导入图片。WIC 接口按本机 SDK 校对，缩放依据 [Microsoft WIC scaling](https://learn.microsoft.com/en-us/windows/win32/wic/-wic-bitmapsources-howto-scale)。

## 3. 迁移与失败保真

SQLite **14 → 15** 仅追加 card-icon-assets 迁移，不改旧迁移文本或 checksum。新增严格类型资产表与 PosterItem 外键列。旧卡片空覆盖，旧 JSON 不因空字段重写。ContentCatalog 编码版本保持 1，新增字段为可选；未知结构仍拒绝，不试图猜测或静默丢弃。

资产 ID 为规范格式版本、宽高及像素的 SHA-256。输入和返回像素均复制，避免调用者修改已验证资产。BLOB、关系版本、content version、命令收据和单 revision 在原事务里一起提交；任一步失败全部回滚。

启动、完整备份及按需读取验证像素和哈希；普通状态事务验证元数据与引用，不每次重扫全部图像。移除卡片、删除海报或恢复默认，只回收无任何卡片引用的自有 BLOB。备份内仍保有它自己的像素，不回收用户文件。

原有迁移前备份、未知 schema 拒绝和恢复入口保持。完整本机备份包含资产及引用。便携 v5 与配置克隆仍明确拒绝真实引用，不允许“导出成功但少了自定义图标”；跨机资产便携不是本轮完成项。

## 4. 代码与测试入口

| 层 | 实现 |
| --- | --- |
| Domain | ContentIconAsset.cs；RealContent.cs 的 PosterItem.IconAssetId |
| Application | ContentIconImport.cs 的准入预算／解码协议；ContentIconCommand.cs 的读集／取消／事务提案；ContentReferenceIntegrity.cs 的最终屏障 |
| Infrastructure | SqliteContentIconStore.cs；SqliteContentRepository.cs 同事务写入／回收；SqliteStateStore.cs 迁移及备份校验 |
| Worker | IconImageDecoder.cs；LocalContentAction.DecodeIcon，复用 LocalContentWorkerClient 的超时／退出 |
| 生产显示 | ReferenceContentRuntime.cs 以 custom:资产ID 区别来源图标；App 注入同库资产读取端口，复用可见缓存与海报绑定 |
| 测试 | ContentIconPersistenceTests.cs 21 项；ContentIconDecodeTests.cs 15 项；ContentIconRuntimeTests.cs 11 项 |
| 应用探针 | App.ContentReferenceProbe.cs：真实 WriteableBitmap 像素读回、恢复默认、A/B 隔离及冷重启；仅隔离 fixture 启用 |

自动验证未新增公共测试入口或日常上传控件。应用探针复用现有 --content-reference-probe，既有测试素材全部在本次隔离目录中。

## 5. 验证与保留证据

最终同包：out/v3-05-v1/candidate03 与 final03。

- App／SessionFixture Release x64 构建：0 警告、0 错误。
- final03/contracts/contracts.trx：**2654 通过，0 失败，0 跳过**，较起点新增 47 项。
- 添加进程 48916：43 断言；同库重启进程 20508：31 断言。两者退出码、错误日志、ActiveResources 均为 0。
- 真实窗口读取规范像素，连续更换／恢复默认／再更换；重启保持。另一海报实体版本、目标和构图不变，介绍区与既有选择回归保持。
- 原生辅助进程测试真实 PNG、静态 JPEG、多尺寸 ICO、透明度及等比缩小；原图字节不变。取消／退出、非法或超限输入、未知库／损坏资产、迁移失败、SQL 故障、备份恢复、共享引用回收及迟到缓存均覆盖。
- App DLL SHA256：AEFF2B64B3EC6CD88D7EE90932CE219A1327ACED12928860D5B93E619CC11333。final03/{sources,artifacts,content-usage}.json 绑定本次源码／脚本和完整候选。
- NativeInput=false、HumanAccepted=false、UserFiles=false。Shell 打开只针对既有测试自建的无界面程序，不执行用户目标。当前无新人工候选驻留。

保留 targeted01 和 full01 原失败：前者整对象比较误把全局投影 revision 变化当成 B 被修改，改为断言 B 实体／实体版本／海报 revision；后者旧测试硬编码 15 为未来 schema，改为独立未知值 999。两处均未放宽生产约束。targeted02 和最终完整回归通过，不覆盖旧包／旧配置或抹除失败记录。

candidate02/final02 的2653项及两应用进程通过记录也保留。收尾审查补上“自定义像素不能冻结来源可用性”的独立Inspect通路，增加失效／恢复测试；targeted03为54项图标相关测试通过，最终重新构建并完整绑定candidate03/final03，不沿用上一包哈希认证新源码。

可复跑命令（必须换用新的独立输出目录）：

~~~powershell
dotnet build src/DesktopUp.App/DesktopUp.App.csproj -c Release -p:Platform=x64 -o <candidate>/app/
dotnet build tests/DesktopUp.SessionFixture/DesktopUp.SessionFixture.csproj -c Release -p:Platform=x64 -o <candidate>/fixture/
pwsh -File scripts/verify-content-usage.ps1 -CandidateDirectory <candidate> -EvidenceRoot <新的项目out目录>
~~~

## 6. 最近下一步

按2026-10-10已确认DEC-039，**先进入[剪切式拖放核心§6.5](../../30_Plans/V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#65-桌面与海报的剪切式拖放主路径)**：05-03/05-07受管存储和意图合同/纯验证 → 05-08账本与恢复保护 → 双向接线 → 连续操作与人工稳定验收。本节V1底层保留，不重做、不扩称物理拖放已实现。

核心稳定出口后再进入 **05-05-V2 所选卡片图标入口**：中心更换图标、只读来源路径、预览确认、恢复默认；接原选择器取消／退出与预览代次，再验即时显示、模板切换、缩框、隐藏重显及备份恢复。之后05-04-V3构造卡片；两项是后移而非取消。

旧介绍文字控件人工未验、R7 携带项和 C2 后置保持，不以这轮自动测试补记人工 PASS。
