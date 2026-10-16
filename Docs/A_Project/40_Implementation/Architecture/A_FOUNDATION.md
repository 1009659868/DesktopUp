# 架构整理 A：目录与依赖边界

日期：2026-10-16；基线 `impl/develop@4ae822a`；功能分支 `impl/architecture-foundation`。
用户授权：先补实施计划，再完成首批目录与依赖整理，保持已验功能，集中验收。本轮不提交/推送/合并，不改发布线。
设计依据：[架构审查](../../20_Architecture/ARCHITECTURE_EVOLUTION_REVIEW_2026-10-16.md)。这是该报告A批的执行计划，不代表B～G同时开工。

## 1. 本批结果与范围

首批以“真实打开链＋启动组装＋验收代码编译边界”为切片，建立可以继续沿用的目录和依赖约束，不全库搬家。

1. **纯文件移动**：将真实内容模型、打开合同/运行、成功事实存储放入职责目录；App及内置场景的独立Probe/Smoke源移到 `tools/review/`。保留原命名空间、类型、文件字节、模块身份、XAML文件和资源路径。清单记录旧/新路径与原始SHA-256，移动后逐项核对。
2. **结构接线**（与移动清单分开）：把服务组装方法原样放到Bootstrap；验收启动分发通过显式 `DesktopUpReviewTools=true` 编入，默认产品构建不包含App场景验收执行器。只隔离探针调用与生命周期钩子，不调整真实操作顺序和算法。必要的生产诊断、文件身份探测与安全账本不当作测试删除。
3. **依赖约束**：复用现有ArchitectureTests，明确六个项目的允许依赖方向；验证正常/验收构建的实际Compile清单。记录仍存在的具体数据库依赖及Worker链接源例外，下一批逐步消除，不本轮伪装成完成解耦。
4. **可维护入口**：build脚本提供显式验收开关；打包固定关闭验收工具。保留旧验收脚本与原始测试，不搬全部脚本、不改变PowerShell相对路径。原生Shell准备诊断与测试可观察钩子本轮按实际依赖保留，不能按名称盲删。

不做：热度计算、模板平台、AI、更新器、数据库schema迁移、新增功能交互、全库namespace改名、重写窗口/拖放/文件恢复、删除历史测试或改用户配置。

## 2. 内部实施顺序（不逐步人工）

| 环节 | 结果 | 退出条件 |
| --- | --- | --- |
| A1 | 目录约定、精确移动清单、原样归组 | 清单路径在工作区内、不覆盖目标，原/新内容哈希相等 |
| A2 | 组装入口、显式验收编译入口、生产依赖门 | 产品/验收两种构建通过；产品不编入独立App验收执行器 |
| A3 | 定向验证、单一候选、集中交付 | 只检查本次编译/依赖/启动与打开链；必要时一次隔离冒烟 |
| 集中人工 | 确认产品体验未改变 | 一次自然操作覆盖海报/中心/模板与内容操作；用户结论单独记录 |

“移动”和“结构接线”在移动清单及差异中分别呈现；未获提交授权不制造提交。如后续授权提交，应分成移动与结构/构建调整两类，不能将行为重写混入rename提交。

## 3. 目录与依赖约定

- 原六个程序集不变；App.xaml/ControlCenterWindow.xaml及其资源名本批不移动。
- Domain/Content：实体与能力规则；Application/Features/Content：打开用例与视图来源；Application/Ports/Content：跨进程/提供方协议。
- Infrastructure/Persistence/Content：打开事实/图标存储；App/Bootstrap：服务组装。
- tools/review/App与tools/review/Modules：仅显式验收构建链接回原App程序集，private partial不跨程序集暴露；不宣称partial已解耦。
- App→Application/Domain/Infrastructure/Windows；Application→Domain；Infrastructure/Windows→Application/Domain；Worker→Application（App到Worker仅构建部署依赖）。
- 暂存例外：App运行期仍有具体SqliteStateStore；Worker仍链接LocalContentReferenceProbe；Shell只读准备诊断与部分观测钩子仍在产品。例外不扩大，后续B/C按真实职责抽取。
- 禁止用新增空接口、万能事件总线或ServiceLocator绕过边界。

## 4. 精简验证与人工

影响面：源码定位、MSBuild Compile集合、启动测试分发、服务组装；文件业务算法/事务/原生输入实现不变。

自动只做：移动哈希/链接校验；正常及ReviewTools构建；现有ArchitectureTests和真实打开相关定向测试；一条隔离启动→海报/中心→正常退出冒烟。若构建或定向测试暴露额外依赖，只扩该失败邻域，不默认跑全量、安装卸载矩阵或全部拖放故障。

集中人工只给一组操作：打开本批独立候选，检查中心和内置模板；用隔离样例做一次打开/右键/拖动，再正常退出重开，确认呈现和保留行为与之前一致。人工未完成前记MANUAL PENDING，不合入develop。
自启动真实登录、真实满盘、长测等既有未验/延期继续携带，不借本批补PASS。

## 5. 实施与证据

- 开工fetch核对：impl/develop与origin/impl/develop一致（4ae822a），已创建并切换impl/architecture-foundation。
- 已保留上轮未提交架构文档；旧DesktopUp仓库的其他工作不动。
- 开工时状态为实施中，当前结果如下。

### A批实施结果

状态：**USER PASS / A批CLOSED**。用户于2026-10-16确认“验证通过”，覆盖下述集中人工自然操作与退出重开。仍在impl/architecture-foundation，未提交/推送/合并；按用户要求继续[B：真实内容用例抽取](B_CONTENT_USE_CASES.md)，不重复A批人工。

- [精确移动清单](A_MOVES.json)：A批完成时75个文件逐字节不变，其中11个生产内容源、64个独立App/模块验收源。哈希是A批纯移动的历史证据；B批后续代码抽取另记，不能再以当前源码执行-VerifyMoveBytes声称仍与A完全相同。
- 新增App/Bootstrap/App.Services.cs：BuildServices方法原样抽出，注册内容/次序/生命周期不变。
- 新增tools/review/App/App.ReviewEntry.cs：原启动分发次序和隔离检查保留；通过可选partial钩子接入。正常编译无该实现，移除独立验收执行器；恢复入口与退出观察钩子按相同位置调用。
- Directory.Build.props/targets与scripts/build.ps1提供DesktopUpReviewTools开关；普通构建默认false，package-preview固定false并设Preview+Review互斥门。模块物理删除验证脚本显式使用ReviewTools。
- 保留例外：部分空观测字段、有限时退出诊断、Shell只读准备仍在生产路径；没有将文件身份检查/安全校验当作测试移除。窗口/具体存储耦合、Worker链接源、旧合同注释仍待B/C，本批不修改语义以保持纯移动可核查。
- 新增[源码职责入口](../../../../src/README.md)与[验收源说明](../../../../tools/review/README.md)；既有smoke/probe脚本使用ReviewTools构建目录，普通候选不能替代探针候选。

### 验证（不跑全量）

| 范围 | 实际结果/证据 |
| --- | --- |
| 纯移动与实际Compile集合 | scripts/check-architecture.ps1 -Offline -VerifyMoveBytes：75/75内容哈希相同；产品98个Compile源、验收163个，其中新增65个验收源；生产源集合一致，无重复/缺失；Preview+Review拒绝符合预期 |
| 正常构建 | scripts/build.ps1 -Offline -OutputDirectory out/architecture-foundation/product02；0警告/0错误 |
| 验收构建 | 同入口加-ReviewTools，out/architecture-foundation/review01；0警告/0错误 |
| 定向测试 | ArchitectureTests＋RealContentOpenTests：39通过；RealContentUsageTests：20通过（真实用例、SQLite事实/重启相关）；共59，不运行其余累计库 |
| IL元数据 | product02不存在RunReviewTools/RunIncomingProbe/RunAutoPostureSmoke/VerifyInlineNamingForProbe；review01存在四者。不是只看项目文件文本判断隔离 |
| 复用UI冒烟 | smoke-incoming.ps1 -AppearanceLayout，out/drop-d3/architecture-ui-smoke01；首次和重启均PASS，实际中心/模板控件、退出activeResources=0；不移动文件 |
| 产品候选冒烟 | product02＋独立architecture-review01/profile：正常启动中心/4海报、退出、再启动退出均PASS；0错误、两次activeResources=0，不调用验收执行器 |
| 人工与环境边界 | 自动进程在工具高权限上下文运行，nativeMouse=false；不代表普通权限拖放、视觉人工或真实登录已验 |

首次product01编译发现两个既有验收回调依赖，补显式可选钩子/条件编译后product02通过；另将仅验收赋值的三个字段显式保持原默认值以消除编译警告。没有修改业务算法来绕过失败。

日志：out/architecture-foundation/product-build.log、review-build.log、fixture-build.log、tests.log、usage-tests.log及tests/*.trx。UI冒烟详细结果见out/drop-d3/architecture-ui-smoke01/results.json。
产品DesktopUp.dll SHA256：C2D6BDD727FF3AC170FE5B42839FCCE094F4F826011C931D9EA9C4FA6878C64B；
验收DLL：0F3CEDAEA55C211176442ED2F7564A92706BD77CA674A1B9D611BB275597BBAA。
两种产物不能互换，人工只用product02。

### 一次集中人工

入口：[start-architecture-review.ps1](../../../../scripts/start-architecture-review.ps1)，默认Open；Status只读，Prepare只准备。候选已绑定上述产品DLL身份，复用已验的四海报真实样例夹具（不是新建四组验收）。配置、受管内容、映射样例均在out/drop-d3/architecture-review01，未创建桌面快捷方式/样例或安装登记；用户真实配置不动。

只验一组自然使用：在A海报选一个样例，打开/右键/内部拖动；在中心查看并切换一个内置模板，确认显示和收展正常；托盘退出后用同一入口重开，检查文件和配置仍在。C/D映射海报不要求重跑原映射/拖放矩阵，不用用户真实文件，也不往真实桌面拖测试样例。

本批样例与已验包保留于隔离out，未向桌面产生需清理的样例。原人工PID 32032仅为当次身份；B批开工未发现DesktopUp运行进程。PowerShell 5.1的Status及实际编译边界检查均通过；服务注册和原验收分发的提取内容另作文本等价核对，文档链接与git diff --check通过。集中人工已由用户确认通过；不扩大为自启真实登录/长测/真实满盘通过，也不据此自动提交或合并。
